namespace CloudFri.Api.Entities;

/// <summary>登录账号。Role: Admin / Member。MemberId 关联成员名单（可空）。</summary>
public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Member";
    public int? MemberId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>成员名单（负责人/团队成员）。Gender: M / F，用于头像。</summary>
public class Member
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Gender { get; set; } = "M";
    public string? Phone { get; set; }
    public string? AvatarPath { get; set; }
    public bool IsActive { get; set; } = true;
    public int Sort { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 项目台账。金额单位：万元。
/// SubmitAmount 按业务类型语义：送审金额 / 控制价 / 预算价 / 建安造价；ApprovedAmount 为审定金额（审核类）。
/// Fee 为咨询费应收（万元），已收由 Receipts 汇总。
/// </summary>
public class Project
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Client { get; set; } = "";
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string BizType { get; set; } = "结算审核";
    public string? Scale { get; set; }
    public string Status { get; set; } = "编制中";
    public int? OwnerMemberId { get; set; }
    public DateOnly? AcceptedDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? ReportDate { get; set; }
    public string? ReportNo { get; set; }
    public decimal? SubmitAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>项目团队分工。Role：项目负责人/编制人/审核人/审定人/配合人员。Duty 为人员职责说明。Share 为工作占比 %。</summary>
public class ProjectMember
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int MemberId { get; set; }
    public string Role { get; set; } = "编制人";
    public string? Duty { get; set; }
    public decimal Share { get; set; }
}

/// <summary>合同。Type：框架合同/单项合同/补充协议。Amount 为空表示按实结算（框架合同）。</summary>
public class Contract
{
    public int Id { get; set; }
    public string No { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "单项合同";
    public string? PartyA { get; set; }
    public decimal? Amount { get; set; }
    public string? AmountNote { get; set; }
    public DateOnly? SignDate { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>项目-合同多对多关联。IsPrimary 标记主合同。</summary>
public class ProjectContract
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ContractId { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>收款记录。Amount 单位：万元。</summary>
public class Receipt
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? Handler { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 成果文件（软件版/文档版/报告）。三类成果同属一个整体版本（DeliverableSet）：
/// 点「新增版本」时当前版本的三类文件一起转历史。Version 为同一版本集合同一分类内的序号；SetId 为空的是整体版本功能上线前的旧文件，按 V1 处理。
/// </summary>
public class ProjectFile
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Category { get; set; } = "Software"; // Software / Document / Report
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public int Version { get; set; } = 1;
    public int? SetId { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}

/// <summary>成果整体版本：一次交付 = 一个版本（软件版+文档版+报告）。</summary>
public class DeliverableSet
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
}

/// <summary>项目过程资料（设计图纸/隐蔽工程资料/工作联系函/会议纪要/其他资料），只增不改版。</summary>
public class ProjectDoc
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Category { get; set; } = "其他资料";
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}

/// <summary>GCFX 解析结果（一期：字符串级可提取字段 + 单位工程构成 JSON）。金额单位：元。</summary>
public class GcfxParse
{
    public int Id { get; set; }
    public int ProjectFileId { get; set; }
    public string? ProjectName { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? AmountCn { get; set; }
    public string? Basis { get; set; }
    public string? Region { get; set; }
    public string? UnitsJson { get; set; }
    public string? Note { get; set; }
    public DateTime ParsedAt { get; set; } = DateTime.Now;
}

public class ContractFile
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}

/// <summary>流程模板定义。SchemaJson 存放 fields 与 opinions 配置（与前端原型一致）。</summary>
public class FormTemplate
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Desc { get; set; }
    public string SchemaJson { get; set; } = "{}";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>流程表单填写记录。DataJson 为字段值字典。</summary>
public class FormRecord
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string No { get; set; } = "";
    public string DataJson { get; set; } = "{}";
    public string? FilledByName { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>资料库文档（规章制度/规范文件/标准图集/其他资料）。</summary>
public class LibraryDoc
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "规章制度";
    public string? Format { get; set; }
    public string StoredPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}

/// <summary>字典项（业务类型、状态、分类等可维护枚举）。</summary>
public class DictItem
{
    public int Id { get; set; }
    public string Group { get; set; } = "";
    public string Name { get; set; } = "";
    public int Sort { get; set; }
    public bool IsActive { get; set; } = true;
}
