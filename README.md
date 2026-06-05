# NL2SQL - 自然语言转 SQL 工具

基于 AI 的自然语言转 SQL 工具，输入中文描述即可生成可执行的 SQL 语句。支持多种大模型（DeepSeek/GPT/Claude）和多种数据库（MySQL/Oracle/SqlServer/PostgreSQL/SQLite），提供命令行（CLI）和图形界面（GUI）两种使用方式。

## ✨ 功能特性

### 🤖 多模型支持
- DeepSeek（默认）
- OpenAI GPT-4o
- Anthropic Claude
- Moonshot (Kimi)
- 通义千问
- Ollama 本地模型
- 任何 OpenAI 兼容 API

### 🗄️ 多数据库支持
- MySQL
- Oracle
- SQL Server
- PostgreSQL
- SQLite

### 📝 SQL 生成功能
- 自然语言输入，SQL 输出
- 自动读取数据库表结构（表名、字段、类型、注释、主键）
- 支持复杂查询：子查询、窗口函数、CTE、多表关联
- 多轮对话支持（追问优化）
- 自动添加行数限制，防止大数据量卡死

### 🛡️ 安全机制
- 禁止执行 DELETE 和 DROP 操作
- INSERT/UPDATE 操作需确认后执行
- 更新前预览当前数据
- 插入前预览将插入的数据
- 查询自动限制行数（可选 100/500/1000/5000/不限制）

### 🖥️ 图形界面功能
- 命名连接管理（同类型数据库可配多个）
- AI 模型切换（顶部下拉框）
- 表搜索过滤（按表名/备注）
- SQL 编辑器（可手动修改再执行）
- 查询结果新窗口展示（自动调整大小）
- 常用 SQL 保存和复用（支持分类、折叠显示）
- SQL 历史记录
- 配置界面（图形化管理模型和连接）

### ⌨️ 命令行功能
- `--models` 列出所有模型
- `--list` 列出所有连接
- `--model` 指定模型
- `--name` 指定连接
- `--schema` 指定表结构文件

---

## 🚀 快速开始

### 环境要求

- .NET 8.0 SDK [下载地址](https://dotnet.microsoft.com/download/dotnet/8.0)
- AI 模型 API Key（DeepSeek/OpenAI/Claude 等）

### 1. 克隆项目

```bash
git clone https://github.com/ZhouTohnny/moxi-nl-sql.git
cd moxi-nl-sql
```

### 2. 创建配置文件

```bash
# 复制配置模板
cp NL2SQL/appsettings.template.json NL2SQL/appsettings.json
cp NL2SQL.GUI/appsettings.template.json NL2SQL.GUI/appsettings.json
```

### 3. 编辑配置

编辑 `NL2SQL/appsettings.json` 和 `NL2SQL.GUI/appsettings.json`，填入真实配置：

```json
{
  "Models": [
    {
      "Name": "DeepSeek",
      "ApiType": "OpenAI",
      "ApiKey": "sk-你的API密钥",
      "BaseUrl": "https://api.deepseek.com",
      "Model": "deepseek-chat"
    }
  ],
  "ActiveModel": "DeepSeek",
  "Connections": [
    {
      "Name": "Oracle 生产",
      "Dialect": "Oracle",
      "ConnectionString": "Data Source=192.168.1.100/ORCL;User Id=myuser;Password=mypassword"
    },
    {
      "Name": "MySQL 本地",
      "Dialect": "MySQL",
      "ConnectionString": "Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=password;"
    }
  ]
}
```

### 4. 运行

**图形界面（推荐）：**
```bash
cd NL2SQL.GUI
dotnet run
```

**命令行：**
```bash
cd NL2SQL
dotnet run -- --name "Oracle 生产"
```

---

## 📖 使用说明

### 图形界面使用

1. **选择模型**：顶部下拉框选择 AI 模型
2. **选择连接**：顶部下拉框选择数据库连接
3. **点击连接**：自动读取表结构
4. **输入描述**：在输入框输入自然语言描述
5. **生成 SQL**：点击「⚡ 生成 SQL」或按 `Ctrl+Enter`
6. **执行 SQL**：点击「▶ 执行」查看结果

### 命令行使用

```bash
# 列出所有模型
dotnet run -- --models

# 列出所有连接
dotnet run -- --list

# 使用指定模型和连接
dotnet run -- --model "DeepSeek" --name "Oracle 生产"

# 交互模式，输入自然语言生成 SQL
dotnet run -- --name "Oracle 生产"
> 查询所有用户的姓名和手机号
> 查询每个部门的员工数量
> quit
```

### 示例输入输出

| 输入 | 输出 |
|------|------|
| 查询所有用户 | `SELECT USER_ID, USER_NAME, PHONE FROM T_USER` |
| 统计每个部门的人数 | `SELECT DEPT_NAME, COUNT(*) FROM EMPLOYEE GROUP BY DEPT_NAME` |
| 查询工资排名前3的员工 | `SELECT * FROM (SELECT ..., ROW_NUMBER() OVER(ORDER BY SALARY DESC) RN FROM EMP) WHERE RN <= 3` |
| 查询工资高于平均的员工 | `SELECT * FROM EMPLOYEE WHERE SALARY > (SELECT AVG(SALARY) FROM EMPLOYEE)` |

---

## 📁 项目结构

```
moxi-nl-sql/
├── README.md                          # 项目说明
├── .gitignore                         # Git 忽略规则
│
├── NL2SQL/                            # 核心库 + CLI
│   ├── Models/
│   │   ├── AppConfig.cs               # 配置文件模型
│   │   ├── DatabaseDialect.cs         # 数据库方言枚举
│   │   ├── SqlHistoryItem.cs          # 历史记录模型
│   │   └── SavedSqlItem.cs            # 常用 SQL 模型
│   ├── Services/
│   │   ├── LLMClient.cs               # AI 客户端（OpenAI/Anthropic）
│   │   ├── SqlGenerator.cs            # SQL 生成器（含 Few-shot 示例）
│   │   ├── SqlExecutor.cs             # SQL 执行器（含行数限制）
│   │   └── SchemaReader.cs            # 数据库表结构读取
│   ├── Program.cs                     # CLI 入口
│   ├── NL2SQL.csproj                  # 项目文件
│   ├── appsettings.template.json      # 配置模板
│   └── README.md                      # CLI 说明
│
└── NL2SQL.GUI/                        # 图形界面
    ├── MainWindow.xaml/.cs            # 主窗口
    ├── SettingsWindow.xaml/.cs        # 配置窗口（模型+连接管理）
    ├── HistoryWindow.xaml/.cs         # SQL 历史记录窗口
    ├── SavedSqlWindow.xaml/.cs        # 常用 SQL 窗口
    ├── ResultWindow.xaml/.cs          # 查询结果窗口
    ├── ConfirmWindow.xaml/.cs         # INSERT/UPDATE 确认窗口
    ├── SaveSqlDialog.xaml/.cs         # 保存常用 SQL 对话框
    ├── TruncateConverter.cs           # 文本截断转换器
    ├── App.xaml/.cs                   # 应用入口
    ├── NL2SQL.GUI.csproj              # 项目文件
    └── appsettings.template.json      # 配置模板
```

---

## ⚙️ 配置说明

### 模型配置

```json
{
  "Name": "模型显示名称",
  "ApiType": "OpenAI 或 Anthropic",
  "ApiKey": "你的 API Key",
  "BaseUrl": "API 地址",
  "Model": "模型名称"
}
```

#### 支持的模型

| 模型 | ApiType | BaseUrl | Model |
|------|---------|---------|-------|
| DeepSeek | OpenAI | https://api.deepseek.com | deepseek-chat |
| GPT-4o | OpenAI | https://api.openai.com | gpt-4o |
| Claude | Anthropic | https://api.anthropic.com | claude-3-5-sonnet-20241022 |
| Moonshot | OpenAI | https://api.moonshot.cn/v1 | moonshot-v1-32k |
| 通义千问 | OpenAI | https://dashscope.aliyuncs.com/compatible-mode/v1 | qwen-turbo |
| Ollama | OpenAI | http://localhost:11434/v1 | qwen2.5:14b |

### 数据库连接配置

```json
{
  "Name": "连接显示名称",
  "Dialect": "MySQL/Oracle/SqlServer/PostgreSQL/SQLite",
  "ConnectionString": "连接字符串"
}
```

#### 连接字符串示例

| 数据库 | 连接字符串 |
|--------|-----------|
| MySQL | `Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=password;` |
| Oracle | `Data Source=192.168.1.100/ORCL;User Id=myuser;Password=mypassword` |
| SqlServer | `Server=localhost;Database=mydb;Trusted_Connection=True;TrustServerCertificate=True;` |
| PostgreSQL | `Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=password;` |
| SQLite | `Data Source=mydb.db;` |

---

## 🔒 安全说明

- `appsettings.json` 包含 API Key 和数据库密码，**已加入 `.gitignore`，不会上传到远程仓库**
- `appsettings.template.json` 使用占位符，可安全提交
- 克隆项目后需自行创建 `appsettings.json` 并填入真实配置
- 程序禁止执行 DELETE 和 DROP 操作
- INSERT/UPDATE 操作需用户确认后才执行

---

## 🛠️ 开发说明

### 构建项目

```bash
# 构建全部
dotnet build

# 构建 CLI
dotnet build NL2SQL/NL2SQL.csproj

# 构建 GUI
dotnet build NL2SQL.GUI/NL2SQL.GUI.csproj
```

### 发布

```bash
# 发布 CLI
dotnet publish NL2SQL/NL2SQL.csproj -c Release -o publish/cli

# 发布 GUI
dotnet publish NL2SQL.GUI/NL2SQL.GUI.csproj -c Release -o publish/gui
```

---

## 📄 许可证

MIT License

---

## 🙏 致谢

- [DeepSeek](https://deepseek.com/) - AI 模型
- [OpenAI](https://openai.com/) - GPT 模型
- [Anthropic](https://anthropic.com/) - Claude 模型
