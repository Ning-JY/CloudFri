using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CloudFri.Api.Services;

/// <summary>
/// GCFX 字符串级解析：GCFX 为 ZIP 包，内含二进制专有记录；以 UTF-16LE 解码提取可读字符串并统计出现频次，
/// 结合频次与形态识别工程名称、造价大写金额（转数字）、计价依据、地区与单位工程构成。
/// 文件内含大量定额模板/枚举字符串，单靠形态会误选，必须用频次区分“实际选用值”与“模板库条目”。
/// 逐行结构化解析（清单/定额/工料机）为二期攻关项。
/// 已验证样例（叙州区观音镇烘干储存场）：总造价 5,591,080.22 与文件大写金额一致。
/// </summary>
public static class GcfxService
{
    public record GcfxResult(string? ProjectName, decimal? TotalAmount, string? AmountCn, string? Basis, string? Region, List<string> Units);

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

        // 工程名称：优先「项目名称：」标注后的内容；否则取含「建设项目」的长串
        string? projectName = null;
        var labeled = strings.Where(s => s.Contains("项目名称") && s.Contains('：') && s.Length - s.IndexOf('：') > 8)
            .OrderByDescending(s => counts[s]).FirstOrDefault();
        if (labeled != null) projectName = labeled[(labeled.IndexOf('：') + 1)..].Trim();
        projectName ??= strings.Where(s => s.Contains("建设项目") && s.Length is >= 10 and <= 80 && !s.Contains("。"))
            .OrderByDescending(s => counts[s]).FirstOrDefault();

        // 单位工程构成：以「工程」结尾、无标点的短串中频次 ≥8 者（真实单位工程会在各报表反复出现，模板枚举只有个位数）
        var units = strings.Where(s => s.EndsWith("工程") && s.Length is >= 4 and <= 20
                && !Regex.IsMatch(s, @"[，。：；、（）()\d]") && counts[s] >= 8 && s != projectName)
            .OrderByDescending(s => counts[s])
            .Take(20).ToList();

        return new GcfxResult(projectName, total, amountCn, basis, region, units);
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
