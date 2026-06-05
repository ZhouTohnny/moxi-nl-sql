# NL2SQL - 自然语言转 SQL 工具

基于 AI 的自然语言转 SQL 工具，支持多种大模型和数据库。支持命令行（CLI）和图形界面（GUI）两种使用方式。

## 功能特性

- 🗣️ 自然语言输入，SQL 输出
- 🤖 多模型支持：DeepSeek、GPT、Claude、本地模型等
- 🗄️ 多数据库支持：MySQL、Oracle、SqlServer、PostgreSQL、SQLite
- 🔌 自动连接数据库，读取表结构
- 📝 SQL 执行、常用 SQL 保存、历史记录
- 🛡️ 安全机制：禁止 DELETE/DROP，INSERT/UPDATE 需确认
- 💬 多轮对话支持
- 📊 复杂查询支持：子查询、窗口函数、CTE

## 前置条件

- .NET 8.0 SDK
- AI 模型 API Key（DeepSeek/OpenAI/Claude 等）

## 快速开始

### 1. 配置

```bash
# 复制配置模板
cp NL2SQL/appsettings.template.json NL2SQL/appsettings.json
cp NL2SQL.GUI/appsettings.template.json NL2SQL.GUI/appsettings.json
```

编辑 `appsettings.json`，填入真实的 API Key 和数据库连接串：

```json
{
  "Models": [
    {
      "Name": "DeepSeek",
      "ApiType": "OpenAI",
      "ApiKey": "你的 API Key",
      "BaseUrl": "https://api.deepseek.com",
      "Model": "deepseek-chat"
    }
  ],
  "ActiveModel": "DeepSeek",
  "Connections": [
    {
      "Name": "Oracle 生产",
      "Dialect": "Oracle",
      "ConnectionString": "Data Source=你的主机:1521/服务名;User Id=用户名;Password=密码"
    }
  ]
}
```

### 2. 运行

```bash
# 命令行模式
cd NL2SQL
dotnet run -- --name "Oracle 生产"

# 图形界面模式
cd NL2SQL.GUI
dotnet run
```

## 命令行用法

```bash
# 列出所有模型和连接
dotnet run -- --models
dotnet run -- --list

# 使用指定模型和连接
dotnet run -- --model "DeepSeek" --name "Oracle 生产"

# 直接指定连接串
dotnet run -- --conn "Server=localhost;Database=mydb;Uid=root;Pwd=123;"
```

## 支持的 AI 模型

| 模型 | ApiType | BaseUrl |
|------|---------|---------|
| DeepSeek | OpenAI | https://api.deepseek.com |
| GPT-4o | OpenAI | https://api.openai.com |
| Claude | Anthropic | https://api.anthropic.com |
| Moonshot (Kimi) | OpenAI | https://api.moonshot.cn/v1 |
| 通义千问 | OpenAI | https://dashscope.aliyuncs.com/compatible-mode/v1 |
| Ollama 本地 | OpenAI | http://localhost:11434/v1 |

## 项目结构

```
NL2SQL/
├── NL2SQL.sln                 # 解决方案
├── .gitignore                 # Git 忽略规则
├── NL2SQL/                    # 核心库 + CLI
│   ├── Models/                # 数据模型
│   ├── Services/              # 核心服务
│   │   ├── LLMClient.cs       # AI 客户端（OpenAI/Anthropic）
│   │   ├── SqlGenerator.cs    # SQL 生成器
│   │   ├── SqlExecutor.cs     # SQL 执行器
│   │   └── SchemaReader.cs    # 表结构读取
│   ├── Program.cs             # CLI 入口
│   └── appsettings.template.json  # 配置模板
│
└── NL2SQL.GUI/                # 图形界面
    ├── MainWindow.xaml/.cs     # 主窗口
    ├── SettingsWindow.xaml/.cs # 配置窗口
    ├── HistoryWindow.xaml/.cs  # 历史记录
    ├── SavedSqlWindow.xaml/.cs # 常用 SQL
    ├── ResultWindow.xaml/.cs   # 查询结果
    └── appsettings.template.json  # 配置模板
```

## 安全说明

- `appsettings.json` 包含敏感信息（API Key、数据库密码），已加入 `.gitignore`
- `appsettings.template.json` 是配置模板，使用占位符，可安全提交
- 克隆项目后需自行创建 `appsettings.json` 并填入真实配置

## 工作原理

1. **连接数据库** → 通过 `INFORMATION_SCHEMA` / 系统视图读取表的元数据
2. **构建提示词** → 表结构 + 数据库方言 + Few-shot 示例 + 用户问题
3. **调用 AI 模型** → 根据上下文生成 SQL
4. **执行 SQL** → 支持查询和增删改，自动限制行数
