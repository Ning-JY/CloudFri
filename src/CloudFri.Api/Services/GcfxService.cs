using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CloudFri.Api.Services;

/// <summary>
/// GCFX 解析：GCFX 为 ZIP 包，内含二进制专有记录。
/// 工程名称/总造价（大写金额）/计价依据/地区以 UTF-16LE 字符串频次法提取（文件内含大量定额模板
/// 枚举字符串，必须用频次区分“实际选用值”与“模板库条目”）。
/// 清单明细与费用汇总走锚定结构解析（2026-10-26 移植自 zaojia 查看器已验证口径）：
/// 0/{单位目录}/3003 记录按「12 位编码 + int32 9 + 9 位父码 + 名称 + 单位 + double 综合单价/合价」
/// 固定布局逐行读取；0/{单位目录}/3010 为费用汇总数字串（分部分项/措施/安全/税前/增值税/总价）。
/// 两样例实测：清单合价逐行求和与费用表分部分项合计差 0.00，单位总价合计与大写总造价一致。
/// 工料机汇总（3007 数量/合价）未稳定映射，不提取、不显示。
/// </summary>
public static class GcfxService
{
    public record GcfxResult(string? ProjectName, decimal? TotalAmount, string? AmountCn, string? Basis, string? Region, List<GcfxUnit> Units, List<GcfxItem> Items);

    /// <summary>清单明细一行：单位工程 / 编码 / 名称 / 计量单位 / 工程量 / 综合单价 / 合价（锚定解析自 3003 记录）。</summary>
    public record GcfxItem(string Unit, string Code, string Name, string UnitName, decimal? Qty, decimal? Price, decimal? Amount);

    /// <summary>单位工程及其费用（3010 费用表，自洽校验通过才有金额：分部分项+措施=税前、税前+增值税=总价）。</summary>
    public record GcfxUnit(string Name, decimal? Boq, decimal? Measures, decimal? Safety, decimal? TaxBase, decimal? Vat, decimal? Total);

    public static GcfxResult Parse(string filePath)
    {
        var counts = ExtractStringCounts(filePath);
        var strings = counts.Keys.ToList();

        // 大写金额：文件中可能有多个（总造价 = 税前造价 + 税金等），总造价取最大值，并保留其大写原文
        var amounts = new List<(decimal V, string S)>();
        foreach (var s in strings)
        {
            foreach (Match m in Regex.Matches(s, @"[零壹贰叁肆伍陆柒捌玖拾佰仟万亿]{2,}元[零壹贰叁肆伍陆柒捌玖角分整]*"))
            {
                var v = ChineseMoneyToDecimal(m.Value);
                if (v != null && v.Value > 0) amounts.Add((v.Value, m.Value));
            }
        }
        var total = amounts.Count == 0 ? (decimal?)null : amounts.Max(x => x.V);
        var amountCn = amounts.Count == 0 ? null : amounts.MaxBy(x => x.V)!.S;

        // 计价依据：形态「××规范清单计价(××定额)××计税」中出现频次最高者（实际选用值会随报表反复出现）
        var basis = strings
            .Where(s => Regex.IsMatch(s, @"规范清单计价\(.*定额\).*计税") && s.Length <= 40)
            .OrderByDescending(s => counts[s]).ThenByDescending(s => s.Length)
            .FirstOrDefault();

        // 地区：先从「建设地点」串锚定城市，再在该城市下选频次最高的行政区划串（排除以“市区”结尾的模板默认项）
        string? city = null;
        var loc = strings.FirstOrDefault(s => s.Contains("建设地点"));
        if (loc != null)
        {
            var cm = Regex.Match(loc, @"([\u4e00-\u9fa5]{2,}市)");
            if (cm.Success) city = cm.Groups[1].Value;
        }
        var regionPool = strings.Where(s => Regex.IsMatch(s, @"^[\u4e00-\u9fa5]{2,}市[\u4e00-\u9fa5]{0,8}(县|区|镇)?$")
            && !s.EndsWith("市区") && !s.Contains("定额") && !s.Contains("工程"));
        if (city != null) regionPool = regionPool.Where(s => s.StartsWith(city));
        var region = regionPool.OrderByDescending(s => counts[s]).FirstOrDefault();

        // 工程名称：优先「项目名称：/工程名称：」标注后的内容（小文件里标注值可能很短，如单项工程导出）；
        // 剥掉标注值开头的序号（如 "1."）；否则取含「建设项目」的长串
        string? projectName = null;
        var labeled = strings
            .Where(s => (s.Contains("项目名称") || s.Contains("工程名称")) && (s.Contains('：') || s.Contains(':')))
            .Select(s =>
            {
                var idx = s.IndexOfAny(new[] { '：', ':' });
                var v = idx >= 0 ? s[(idx + 1)..].Trim() : "";
                v = Regex.Replace(v, @"^\d+[\.、]\s*", "");
                return (Orig: s, Val: v);
            })
            .Where(x => x.Val.Length >= 2)
            .OrderByDescending(x => counts[x.Orig])
            .FirstOrDefault();
        projectName = labeled.Val;
        projectName ??= strings.Where(s => s.Contains("建设项目") && s.Length is >= 10 and <= 80 && !s.Contains("。"))
            .OrderByDescending(s => counts[s]).FirstOrDefault();

        // 单位工程构成：以「工程」结尾、无标点的短串，按频次两档提取——
        // 常规文件用 ≥8（真实单位工程在各报表反复出现；模板枚举多在 4–7 次，实测会混入城市轨道交通等定额类别）；
        // 若 ≥8 一个都提不到（小文件/单项导出，如只含一个单位工程的 SGCFX，真实串频次只有 4），再放宽到 ≥4。
        var unitPool = strings.Where(s => s.EndsWith("工程") && s.Length is >= 4 and <= 20
                && !Regex.IsMatch(s, @"[，。：；、（）()\d]") && s != projectName).ToList();
        var legacyNames = unitPool.Where(s => counts[s] >= 8)
            .OrderByDescending(s => counts[s]).Take(20).ToList();
        if (legacyNames.Count == 0)
            legacyNames = unitPool.Where(s => counts[s] >= 4)
                .OrderByDescending(s => counts[s]).Take(20).ToList();

        var (units, items) = ExtractStructured(filePath, legacyNames);
        return new GcfxResult(projectName, total, amountCn, basis, region, units, items);
    }

    // ---------- 结构化解析（锚定布局） ----------
    // GCFX 内部结构：0/Tree 为工程树节点表（节点名 UTF-16LE 串，串起始前 29 字节处 UInt16 为节点号，
    // 与数据目录号一致，如 0/4/3003）；0/{节点号}/3003 为该单位工程的分部分项记录（锚定布局见
    // ExtractUnitItemsAnchored）；0/{节点号}/3010 为该单位工程费用汇总（数字串，见 ParseFeeEntry）。
    // 单位工程名按工程树顺序与目录号对应，标签取「单项工程 · 单位工程」（如 仓库 · 电力工程）。

    private static (List<GcfxUnit> Units, List<GcfxItem> Items) ExtractStructured(string filePath, List<string> legacyNames)
    {
        var units = new List<GcfxUnit>();
        var items = new List<GcfxItem>();
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var unitDirs = new SortedSet<int>();
            foreach (var e in zip.Entries)
            {
                var m = Regex.Match(e.FullName, @"^0/(\d+)/3003$");
                if (m.Success) unitDirs.Add(int.Parse(m.Groups[1].Value));
            }
            if (unitDirs.Count == 0)
                return (legacyNames.Select(n => new GcfxUnit(n, null, null, null, null, null, null)).ToList(), items);

            var labels = ParseTreeLabels(zip, unitDirs);
            foreach (var dir in unitDirs)
            {
                var label = labels.TryGetValue(dir, out var lb) ? lb : $"单位工程（目录 {dir}）";
                var data = ReadEntry(zip, $"0/{dir}/3003");
                var rows = data != null ? ExtractUnitItemsAnchored(data, label) : new List<GcfxItem>();
                if (rows.Count == 0 && data != null)
                    rows = ExtractUnitItems(data, label); // 锚定未命中时回退旧字符串法（无单价/合价）
                items.AddRange(rows);

                var feeData = ReadEntry(zip, $"0/{dir}/3010");
                var fee = feeData != null ? ParseFeeEntry(feeData) : null;
                units.Add(fee != null
                    ? new GcfxUnit(label, fee.Value.Boq, fee.Value.Measures, fee.Value.Safety, fee.Value.TaxBase, fee.Value.Vat, fee.Value.Total)
                    : new GcfxUnit(label, null, null, null, null, null, null));
            }
        }
        catch { /* 结构化解析失败不影响主解析结果（名称/总造价等） */ }
        if (units.Count == 0)
            units = legacyNames.Select(n => new GcfxUnit(n, null, null, null, null, null, null)).ToList();
        return (units, items);
    }

    private static byte[]? ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry == null) return null;
        using var ms = new MemoryStream();
        entry.Open().CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>3010 费用汇总：按 UTF-16LE 数字串扫描（不要求对齐），取形如 1234.56 的串；
    /// 每单位恰好 6 个，顺序为：分部分项合计、措施项目合计、安全生产措施费、税前合计、增值税、总价。
    /// 仅当 分部分项+措施=税前、税前+增值税=总价（容差 0.02）时才采信，否则返回 null 不显示金额。</summary>
    private static (decimal Boq, decimal Measures, decimal Safety, decimal TaxBase, decimal Vat, decimal Total)? ParseFeeEntry(byte[] data)
    {
        var amounts = new List<decimal>();
        var n = data.Length;
        var i = 0;
        while (i + 1 < n)
        {
            if (IsDigitByte(data[i]) && data[i + 1] == 0)
            {
                var j = i;
                var sb = new StringBuilder();
                while (j + 1 < n && ((IsDigitByte(data[j]) && data[j + 1] == 0) || (data[j] == 0x2E && data[j + 1] == 0)))
                {
                    sb.Append((char)data[j]);
                    j += 2;
                }
                var s = sb.ToString();
                if (Regex.IsMatch(s, @"^\d+\.\d{2}$") && decimal.TryParse(s, out var v)) amounts.Add(v);
                i = j;
            }
            else i++;
        }
        if (amounts.Count < 6) return null;
        var fee = (Boq: amounts[0], Measures: amounts[1], Safety: amounts[2], TaxBase: amounts[3], Vat: amounts[4], Total: amounts[5]);
        var ok = Math.Abs(fee.Boq + fee.Measures - fee.TaxBase) < 0.02m
              && Math.Abs(fee.TaxBase + fee.Vat - fee.Total) < 0.02m;
        return ok ? fee : null;
    }

    private static bool IsDigitByte(byte b) => b is >= 0x30 and <= 0x39;

    private static bool Is12DigitCode(byte[] data, int off)
    {
        if (off + 24 > data.Length) return false;
        for (var k = 0; k < 12; k++)
            if (!IsDigitByte(data[off + 2 * k]) || data[off + 2 * k + 1] != 0) return false;
        return true;
    }

    private static string MapUnit(string raw) => raw switch { "m3" => "m³", "m2" => "m²", _ => raw };

    private static readonly int[] QtyDeltas = { -53, -45, -61, -37, -69, -29, -77, -21 };

    /// <summary>3003 锚点解析：编码(12 位 UTF-16LE) + int32 9 + 9 位父码(=编码前 9 位) + int32 名称长度 + 名称
    /// + int32 单位长度 + 单位 + double 综合单价 +（隔 4 字节）double 未取整合价 + double 合价。
    /// 工程量：先在行首前若干固定偏移找成对 double 且 量×价 取整=合价；未命中用 未取整合价÷单价 反推（3 位小数）。</summary>
    private static List<GcfxItem> ExtractUnitItemsAnchored(byte[] data, string label)
    {
        var rows = new List<GcfxItem>();
        var n = data.Length;
        for (var off = 0; off + 80 < n; off++)
        {
            if (!Is12DigitCode(data, off)) continue;
            var code = Encoding.Unicode.GetString(data, off, 24);
            if (off + 28 > n) continue;
            if (BitConverter.ToInt32(data, off + 24) != 9) continue;
            var parent = Encoding.Unicode.GetString(data, off + 28, 18);
            if (parent != code[..9]) continue;
            var nameLenOff = off + 46;
            if (nameLenOff + 4 > n) continue;
            var nameLen = BitConverter.ToInt32(data, nameLenOff);
            if (nameLen <= 0 || nameLen > 200) continue;
            var nameOff = nameLenOff + 4;
            if (nameOff + nameLen * 2 > n) continue;
            var name = Encoding.Unicode.GetString(data, nameOff, nameLen * 2);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var unitLenOff = nameOff + nameLen * 2;
            if (unitLenOff + 4 > n) continue;
            var unitLen = BitConverter.ToInt32(data, unitLenOff);
            if (unitLen <= 0 || unitLen > 20) continue;
            var unitOff = unitLenOff + 4;
            if (unitOff + unitLen * 2 > n) continue;
            var unitRaw = Encoding.Unicode.GetString(data, unitOff, unitLen * 2);
            var unitEnd = unitOff + unitLen * 2;
            if (unitEnd + 28 > n) continue;
            var price = BitConverter.ToDouble(data, unitEnd);
            var amtUnrounded = BitConverter.ToDouble(data, unitEnd + 12);
            var amount = BitConverter.ToDouble(data, unitEnd + 20);
            if (!double.IsFinite(price) || !double.IsFinite(amount) || amount < 0) continue;

            double? qty = null;
            foreach (var delta in QtyDeltas)
            {
                var qOff = off + delta;
                if (qOff < 0 || qOff + 16 > n) continue;
                var q1 = BitConverter.ToDouble(data, qOff);
                var q2 = BitConverter.ToDouble(data, qOff + 8);
                if (q1 != q2 || !double.IsFinite(q1) || q1 < 0) continue;
                if (Math.Abs(Math.Round(q1 * price * 100) / 100 - amount) < 0.015) { qty = q1; break; }
            }
            if (qty == null && price > 0 && double.IsFinite(amtUnrounded))
            {
                var q = Math.Round(amtUnrounded / price * 1000) / 1000;
                if (double.IsFinite(q) && q >= 0) qty = q;
            }

            rows.Add(new GcfxItem(label, code, name, MapUnit(unitRaw),
                qty.HasValue ? (decimal)qty.Value : null, (decimal)price, decimal.Round((decimal)amount, 2)));
            off = unitEnd + 28 - 1; // 跳过已解析记录头，避免在名称/单位内重复命中
        }
        return rows;
    }

    private static bool IsCjk(ushort v) => v is >= 0x4E00 and <= 0x9FA5;

    /// <summary>解析工程树：按字节序扫描节点名（两种字节对齐都要扫），节点号 = 名串起始前 29 字节的 UInt16。</summary>
    private static Dictionary<int, string> ParseTreeLabels(ZipArchive zip, SortedSet<int> unitDirs)
    {
        var labels = new Dictionary<int, string>();
        var tree = zip.GetEntry("0/Tree");
        if (tree == null) return labels;
        using var ms = new MemoryStream();
        tree.Open().CopyTo(ms);
        var data = ms.ToArray();

        var nodes = new List<(int Start, int Id, string Name)>();
        var seen = new HashSet<int>();
        for (var parity = 0; parity <= 1; parity++)
        {
            var i = parity;
            while (i + 1 < data.Length)
            {
                var v = BitConverter.ToUInt16(data, i);
                if (IsCjk(v))
                {
                    var start = i;
                    var sb = new StringBuilder();
                    while (i + 1 < data.Length)
                    {
                        var ch = BitConverter.ToUInt16(data, i);
                        if (IsCjk(ch) || ch is 0x3001 or 0xFF08 or 0xFF09) { sb.Append((char)ch); i += 2; }
                        else break;
                    }
                    if (sb.Length >= 2 && seen.Add(start))
                    {
                        var id = start >= 29 ? BitConverter.ToUInt16(data, start - 29) : -1;
                        nodes.Add((start, id, sb.ToString()));
                    }
                    continue;
                }
                i += 2;
            }
        }
        nodes.Sort((a, b) => a.Start.CompareTo(b.Start));

        string? parent = null;
        foreach (var n in nodes)
        {
            if (unitDirs.Contains(n.Id))
                labels[n.Id] = parent != null ? $"{parent} · {n.Name}" : n.Name;
            else if (n.Id > 0)
                parent = n.Name; // 单项工程节点（其目录下无 3003 分部分项数据）
        }
        return labels;
    }

    private static readonly Regex ItemRunRegex = new(@"[\u4e00-\u9fa5A-Za-z0-9\.\-（）()·、，：:；/%]{1,}", RegexOptions.Compiled);
    private static readonly Regex QtyRegex = new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

    private static List<GcfxItem> ExtractUnitItems(byte[] data, string label)
    {
        var result = new List<GcfxItem>();
        var i = 0;
        while (i + 24 <= data.Length)
        {
            if (!IsDigitAt(data, i)) { i++; continue; }
            // 连续 12 个 UTF-16LE 数字字符 = 清单编码
            var ok = true;
            for (var k = 0; k < 12; k++)
                if (!IsDigitAt(data, i + k * 2)) { ok = false; break; }
            if (!ok) { i++; continue; }
            var code = Encoding.Unicode.GetString(data, i, 24);
            var after = Encoding.Unicode.GetString(data, i, Math.Min(400, data.Length - i));
            var runs = ItemRunRegex.Matches(after).Select(m => m.Value).ToList();
            if (runs.Count >= 3 && runs[0] == code && runs[1] == code[..9] && runs[2].Length >= 2 && runs[2].Any(c => c is >= '一' and <= '鿿'))
            {
                var unitName = runs.Count >= 4 && runs[3].Length <= 16 ? runs[3] : "";
                var qty = FindQtyBefore(data, i);
                result.Add(new GcfxItem(label, code, runs[2], unitName, qty, null, null));
                i += 24;
            }
            else i++;
        }
        return result;
    }

    private static bool IsDigitAt(byte[] data, int bytePos) =>
        bytePos + 1 < data.Length && data[bytePos] is >= 0x30 and <= 0x39 && data[bytePos + 1] == 0;

    /// <summary>工程量 = 编码前最近的纯数字串（两种字节对齐都解码，按串的结束位置取最近）。</summary>
    private static decimal? FindQtyBefore(byte[] data, int codeBytePos)
    {
        var start = Math.Max(0, codeBytePos - 800);
        var len = codeBytePos - start;
        if (len <= 0) return null;
        decimal? best = null;
        var bestEnd = -1;
        for (var shift = 0; shift <= 1; shift++)
        {
            var s = Encoding.Unicode.GetString(data, start + shift, len - shift);
            foreach (Match m in ItemRunRegex.Matches(s))
            {
                var g = m.Value.Trim();
                if (!QtyRegex.IsMatch(g)) continue;
                if (!decimal.TryParse(g, out var q) || q <= 0 || q > 100_000_000m) continue;
                var end = start + shift + (m.Index + m.Length) * 2;
                if (end > bestEnd) { bestEnd = end; best = q; }
            }
        }
        return best;
    }

    private static Dictionary<string, int> ExtractStringCounts(string filePath)
    {
        var counts = new Dictionary<string, int>();
        using var zip = ZipFile.OpenRead(filePath);
        foreach (var entry in zip.Entries)
        {
            if (entry.Length == 0 || entry.Length > 8 * 1024 * 1024) continue;
            using var ms = new MemoryStream();
            entry.Open().CopyTo(ms);
            var text = Encoding.Unicode.GetString(ms.ToArray());
            foreach (Match m in Regex.Matches(text, @"[\u4e00-\u9fa5A-Za-z0-9\.\-（）()·、，：:；/ ]{4,}"))
            {
                var s = m.Value.Trim();
                if (s.Length >= 4) counts[s] = counts.TryGetValue(s, out var c) ? c + 1 : 1;
            }
        }
        return counts;
    }

    /// <summary>中文大写金额转数字（支持万/亿/拾佰仟与角分）。</summary>
    public static decimal? ChineseMoneyToDecimal(string s)
    {
        var digits = new Dictionary<char, int>
        {
            ['零'] = 0, ['壹'] = 1, ['贰'] = 2, ['叁'] = 3, ['肆'] = 4,
            ['伍'] = 5, ['陆'] = 6, ['柒'] = 7, ['捌'] = 8, ['玖'] = 9
        };
        try
        {
            var yuanPart = s.Split('元')[0];
            decimal section = 0, number = 0, total = 0;
            foreach (var ch in yuanPart)
            {
                if (digits.TryGetValue(ch, out var d)) { number = d; continue; }
                switch (ch)
                {
                    case '拾': section += (number == 0 ? 1 : number) * 10; number = 0; break;
                    case '佰': section += number * 100; number = 0; break;
                    case '仟': section += number * 1000; number = 0; break;
                    case '万': section = (section + number) * 10000; total += section; section = 0; number = 0; break;
                    case '亿': section = (section + number) * 100000000; total += section; section = 0; number = 0; break;
                }
            }
            total += section + number;

            var frac = 0m;
            var jiao = Regex.Match(s, @"([零壹贰叁肆伍陆柒捌玖])角");
            if (jiao.Success) frac += digits[jiao.Groups[1].Value[0]] * 0.1m;
            var fen = Regex.Match(s, @"([零壹贰叁肆伍陆柒捌玖])分");
            if (fen.Success) frac += digits[fen.Groups[1].Value[0]] * 0.01m;
            return total + frac;
        }
        catch { return null; }
    }
}
