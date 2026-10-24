using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Services;

/// <summary>首次启动种子：管理员账号（密码来自配置 Seed:AdminPassword，生产必须用环境变量覆盖）、
/// 成员名单、字典、流程模板三件套。已存在数据时跳过。</summary>
public static class SeedData
{
    public static async Task RunAsync(AppDbContext db, IConfiguration cfg)
    {
        if (!await db.Users.AnyAsync())
        {
            var hasher = new PasswordHasher<User>();
            var admin = new User
            {
                UserName = cfg["Seed:AdminUser"] ?? "admin",
                DisplayName = "管理员",
                Role = "Admin"
            };
            admin.PasswordHash = hasher.HashPassword(admin, cfg["Seed:AdminPassword"] ?? "cloudfri123");
            db.Users.Add(admin);
        }

        if (!await db.Members.AnyAsync())
        {
            var people = new (string Name, string Gender)[]
            {
                ("甯金元", "M"), ("王建国", "M"), ("李秀英", "F"), ("张明轩", "M"), ("陈思远", "M"),
                ("刘春梅", "F"), ("赵雪", "F"), ("周强", "M"), ("吴敏", "F"), ("郑伟", "M"), ("林小雨", "F")
            };
            for (var i = 0; i < people.Length; i++)
                db.Members.Add(new Member { Name = people[i].Name, Gender = people[i].Gender, Sort = i + 1 });
        }

        if (!await db.DictItems.AnyAsync())
        {
            var groups = new Dictionary<string, string[]>
            {
                ["BizType"] = new[] { "结算审核", "预算编制", "清单控制价", "竣工决算", "跟踪审计", "全过程咨询" },
                ["ProjectStatus"] = new[] { "编制中", "审核中", "已出报告", "已归档" },
                ["ContractType"] = new[] { "框架合同", "单项合同", "补充协议" },
                ["TeamRole"] = new[] { "项目负责人", "编制人", "审核人", "审定人", "配合人员" },
                ["DocCategory"] = new[] { "设计图纸", "隐蔽工程资料", "工作联系函", "会议纪要", "其他资料" },
                ["LibCategory"] = new[] { "规章制度", "规范文件", "标准图集", "其他资料" }
            };
            foreach (var (g, names) in groups)
                for (var i = 0; i < names.Length; i++)
                    db.DictItems.Add(new DictItem { Group = g, Name = names[i], Sort = i + 1 });
        }

        if (!await db.FormTemplates.AnyAsync())
        {
            db.FormTemplates.AddRange(
                new FormTemplate
                {
                    Key = "seal", Name = "盖章申请表", Code = "YZ", Sort = 1,
                    Desc = "对外报送、合同文件等用印申请，审批意见栏打印后手签。",
                    SchemaJson = """{"byKey":"applicant","fields":[{"k":"dept","lb":"申请部门","ph":"如：造价咨询一部"},{"k":"applicant","lb":"申请人","ph":"姓名"},{"k":"reason","lb":"用印事由","ph":"说明用印事由","w":2},{"k":"sealType","lb":"印章类别","type":"sel","opts":["公司公章","合同专用章","财务专用章","法人章"]},{"k":"copies","lb":"用印文件份数","ph":"如：3 份"},{"k":"docName","lb":"用印文件名称","ph":"文件名称","w":2},{"k":"sendTo","lb":"文件报送单位","ph":"报送单位"},{"k":"date","lb":"申请日期","type":"date"}],"opinions":["部门负责人意见","分管领导意见","总经理意见","印章保管员存档确认"]}"""
                },
                new FormTemplate
                {
                    Key = "expense", Name = "费用报销单", Code = "BX", Sort = 2,
                    Desc = "差旅、办公、招待等费用报销，含费用类别、票据张数与收款方式。",
                    SchemaJson = """{"byKey":"person","fields":[{"k":"person","lb":"报销人","ph":"姓名"},{"k":"dept","lb":"所属部门","ph":"如：造价咨询二部"},{"k":"reason","lb":"报销事由","ph":"说明报销事由","w":2},{"k":"cat","lb":"费用类别","type":"sel","opts":["差旅费","办公费","业务招待费","交通费","其他"]},{"k":"amount","lb":"报销金额（元）","ph":"0.00"},{"k":"bills","lb":"票据张数","ph":"如：5 张"},{"k":"payway","lb":"收款方式","type":"sel","opts":["银行转账","现金","冲抵借款"]},{"k":"date","lb":"报销日期","type":"date"}],"opinions":["部门负责人意见","财务审核意见","总经理意见"]}"""
                },
                new FormTemplate
                {
                    Key = "leave", Name = "请假申请表", Code = "QJ", Sort = 3,
                    Desc = "事假、病假、年休假、调休等请假申请。",
                    SchemaJson = """{"byKey":"name","fields":[{"k":"name","lb":"申请人","ph":"姓名"},{"k":"dept","lb":"所属部门","ph":"部门"},{"k":"kind","lb":"请假类别","type":"sel","opts":["事假","病假","年休假","调休","其他"]},{"k":"days","lb":"请假天数","ph":"如：2 天"},{"k":"range","lb":"请假时间","ph":"如：10月28日—10月29日","w":2},{"k":"reason","lb":"请假事由","ph":"说明事由","w":2},{"k":"date","lb":"申请日期","type":"date"}],"opinions":["部门负责人意见","分管领导意见"]}"""
                });
        }

        await db.SaveChangesAsync();
    }
}
