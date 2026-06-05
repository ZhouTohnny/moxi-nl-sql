# NL2SQL - 自然语言转 SQL 工具

基于 DeepSeek API，将自然语言描述转换为 SQL 查询语句。支持命令行（CLI）和图形界面（GUI）两种使用方式。

## 功能特性

- 🗣️ 自然语言输入，SQL 输出
- 🗄️ 支持 MySQL、Oracle、SqlServer、PostgreSQL、SQLite
- 🔌 **自动连接数据库，读取表结构**
- 📄 也支持手动导入 DDL 文件
- 🖥️ 命令行 + 图形界面双模式
- ⚡ 基于 DeepSeek API，响应快速

## 前置条件

- .NET 8.0 SDK
- DeepSeek API Key（[申请地址](https://platform.deepseek.com/)）

## 快速开始

### 1. 配置

编辑 `appsettings.json`，填入 API Key 和数据库连接串：

```json
{
  "DeepSeek": {
    "ApiKey": "sk-你的密钥"
  },
  "Databases": {
    "MySQL": {
      "ConnectionString": "Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=password;"
    },
    "SqlServer": {
      "ConnectionString": "Server=localhost;Database=mydb;Trusted_Connection=True;"
    }
  }
}
```

### 2. 运行

```bash
# 命令行模式
cd NL2SQL
dotnet run -- --db mysql

# 图形界面模式
cd NL2SQL.GUI
dotnet run
```

## 两种使用方式

### 命令行 (CLI)

```bash
# 使用配置文件中的连接串
dotnet run -- --db mysql

# 切换到 Oracle
dotnet run -- --db oracle

# 命令行覆盖连接串
dotnet run -- --db mysql --conn "Server=192.168.1.100;Database=test;Uid=root;Pwd=123;"
```

### 图形界面 (GUI)

启动后：
1. 选择数据库类型（下拉框）
2. 点击「连接数据库」自动读取表结构
3. 左侧显示所有数据表，点击可查看表结构
4. 输入框输入自然语言描述
5. 点击「生成 SQL」或按 `Ctrl+Enter`

## 项目结构

```
Video/
├── NL2SQL.sln                 # 解决方案文件
├── NL2SQL/                    # 命令行项目
│   ├── Models/
│   │   ├── AppConfig.cs       # 配置文件模型
│   │   └── DatabaseDialect.cs # 数据库方言枚举
│   ├── Services/
│   │   ├── DeepSeekClient.cs  # DeepSeek API 客户端
│   │   ├── SqlGenerator.cs    # SQL 生成逻辑
│   │   └── SchemaReader.cs    # 数据库元数据读取
│   ├── Program.cs             # CLI 入口
│   └── appsettings.json       # 配置文件
│
└── NL2SQL.GUI/                # 图形界面项目
    ├── MainWindow.xaml         # WPF 界面
    ├── MainWindow.xaml.cs      # 界面逻辑
    ├── App.xaml                # 应用入口
    └── appsettings.json        # 配置文件（与 CLI 共享格式）
```

## 工作原理

1. **连接数据库** → 通过 `INFORMATION_SCHEMA` / 系统视图读取所有表的元数据（表名、字段、类型、注释、主键）
2. **构建提示词** → 将表结构 + 数据库方言差异 + 用户问题组装成 prompt
3. **调用 DeepSeek** → LLM 根据上下文生成精准的 SQL
4. **返回结果** → 输出可直接执行的 SQL 语句
