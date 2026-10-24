# CloudFri 造价咨询台账系统

前后端分离：后端 ASP.NET Core 10 Web API + EF Core 9 + Pomelo MySQL；前端 Vue 3 + Vite + Naive UI + ECharts（web/）。

## 结构
- `src/CloudFri.Api/` 后端 API（实体、DbContext、JWT 登录、种子数据、EF 迁移）
- `web/` 前端（Vue 3）
- 部署：Docker（api + web/nginx + mysql），见后续 docker-compose.yml

## 约定
- 金额单位：项目/合同/收款均为「万元」（decimal 18,2）；GCFX 解析总额为「元」。
- 管理员初始账号来自配置 `Seed:AdminUser/Seed:AdminPassword`，生产环境必须用环境变量覆盖（Seed__AdminPassword、Jwt__Key、ConnectionStrings__Default）。
- schema 变更走 EF 迁移：`dotnet ef migrations add <Name> --project src/CloudFri.Api --output-dir Data/Migrations`，禁止手写迁移类漏特性（参考 EnterpriseMS 教训：手写迁移必须同时标注 [Migration] 与 [DbContextAttribute]）。
- 本地 SDK 位于 ~/dotnet，运行 dotnet-ef 需 `DOTNET_ROOT=~/dotnet`。
