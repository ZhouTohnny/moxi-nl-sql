using System.Text.RegularExpressions;
using NL2SQL.Models;

namespace NL2SQL.Services;

/// <summary>
/// 自然语言转 SQL 生成器
/// </summary>
public class SqlGenerator
{
    private readonly ILLMClient _client;
    private readonly DatabaseDialect _dialect;
    private readonly string _systemPrompt;
    private readonly string? _schema;
    private readonly List<(string Role, string Content)> _conversationHistory = new();

    public SqlGenerator(DatabaseDialect dialect, string apiKey, string? baseUrl = null, string? model = null, string? schema = null, string? apiType = null)
    {
        _dialect = dialect;
        _schema = schema;
        _client = LLMClientFactory.Create(
            apiType ?? "OpenAI",
            apiKey,
            baseUrl ?? "https://api.deepseek.com",
            model ?? "deepseek-chat"
        );
        _systemPrompt = BuildSystemPrompt(schema);
    }

    /// <summary>
    /// 获取当前表结构
    /// </summary>
    public string? GetSchema() => _schema;

    private string BuildSystemPrompt(string? schema)
    {
        var dialectPrompt = DialectConfig.GetDialectPrompt(_dialect);
        var examples = GetFewShotExamples();

        var prompt = $"""
            {dialectPrompt}

            你是一个资深数据库专家，擅长将自然语言转换为高质量的 SQL 查询。

            【核心规则】
            1. 只输出纯 SQL 语句，不要用 markdown 代码块包裹，不要输出任何解释
            2. 使用标准的 SQL 编写风格，关键字大写
            3. 禁止使用 SELECT *，必须明确列出所有需要查询的字段名
            4. 当用户说"查询某表"或没有指定具体字段时，列出该表的全部字段
            5. 如果用户是在之前的 SQL 基础上修改，请基于上一条 SQL 进行调整

            【复杂查询能力】
            你必须能够处理以下复杂场景：

            1. 子查询：
               - 标量子查询：SELECT ... WHERE col = (SELECT ...)
               - IN 子查询：SELECT ... WHERE col IN (SELECT ...)
               - EXISTS 子查询：SELECT ... WHERE EXISTS (SELECT ...)
               - 派生表：SELECT ... FROM (SELECT ...) AS alias

            2. 窗口函数：
               - ROW_NUMBER()：行号排名
               - RANK() / DENSE_RANK()：并列排名
               - SUM() OVER()：累计求和
               - AVG() OVER()：移动平均
               - LAG() / LEAD()：前后行对比
               - NTILE()：分桶

            3. 公用表表达式 (CTE)：
               - WITH cte AS (SELECT ...) SELECT ... FROM cte

            4. 高级聚合：
               - GROUP BY + HAVING
               - ROLLUP / CUBE
               - GROUPING SETS

            5. 时间日期处理：
               - 日期范围过滤
               - 日期函数（YEAR/MONTH/DAY/DATE_FORMAT 等）
               - 日期加减运算

            【Few-shot 示例】

            {examples}

            【输出要求】
            - 生成的 SQL 必须语法正确，可直接执行
            - 使用表结构中的实际表名和字段名
            - 添加简洁的中文注释说明查询逻辑
            - 复杂查询使用 CTE 或子查询保持可读性
            """;

        if (!string.IsNullOrEmpty(schema))
        {
            prompt += $"""

                【数据库表结构】
                {schema}

                请严格使用上述表结构中的表名和字段名生成 SQL。
                """;
        }

        return prompt;
    }

    /// <summary>
    /// 获取 Few-shot 示例
    /// </summary>
    private string GetFewShotExamples() => _dialect switch
    {
        DatabaseDialect.Oracle => """
            示例1 - 简单查询：
            用户：查询所有员工的姓名和工资
            SELECT EMP_NAME, SALARY FROM EMPLOYEE;

            示例2 - 多表关联：
            用户：查询每个员工的姓名和部门名称
            SELECT E.EMP_NAME, D.DEPT_NAME
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID;

            示例3 - 子查询：
            用户：查询工资高于平均工资的员工
            SELECT EMP_NAME, SALARY
            FROM EMPLOYEE
            WHERE SALARY > (SELECT AVG(SALARY) FROM EMPLOYEE);

            示例4 - 窗口函数排名：
            用户：查询每个部门工资排名前3的员工
            SELECT DEPT_NAME, EMP_NAME, SALARY
            FROM (
                SELECT D.DEPT_NAME, E.EMP_NAME, E.SALARY,
                       ROW_NUMBER() OVER(PARTITION BY E.DEPT_ID ORDER BY E.SALARY DESC) AS RN
                FROM EMPLOYEE E
                LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            )
            WHERE RN <= 3;

            示例5 - 累计求和：
            用户：查询每个月的销售额和累计销售额
            SELECT SALE_MONTH, AMOUNT,
                   SUM(AMOUNT) OVER(ORDER BY SALE_MONTH) AS CUM_AMOUNT
            FROM MONTHLY_SALES;

            示例6 - 同比增长：
            用户：查询每个月的销售额和同比增长率
            SELECT SALE_MONTH, AMOUNT,
                   LAG(AMOUNT, 12) OVER(ORDER BY SALE_MONTH) AS LAST_YEAR_AMOUNT,
                   ROUND((AMOUNT - LAG(AMOUNT, 12) OVER(ORDER BY SALE_MONTH)) /
                         LAG(AMOUNT, 12) OVER(ORDER BY SALE_MONTH) * 100, 2) AS GROWTH_RATE
            FROM MONTHLY_SALES;

            示例7 - CTE + 多层嵌套：
            用户：查询连续3个月都有订单的客户
            WITH MONTHLY_ORDERS AS (
                SELECT CUSTOMER_ID,
                       TO_CHAR(ORDER_DATE, 'YYYY-MM') AS ORDER_MONTH,
                       COUNT(*) AS ORDER_COUNT
                FROM ORDERS
                GROUP BY CUSTOMER_ID, TO_CHAR(ORDER_DATE, 'YYYY-MM')
            ),
            CONSECUTIVE AS (
                SELECT CUSTOMER_ID, ORDER_MONTH,
                       LAG(ORDER_MONTH, 1) OVER(PARTITION BY CUSTOMER_ID ORDER BY ORDER_MONTH) AS PREV_MONTH,
                       LAG(ORDER_MONTH, 2) OVER(PARTITION BY CUSTOMER_ID ORDER BY ORDER_MONTH) AS PREV_2_MONTH
                FROM MONTHLY_ORDERS
            )
            SELECT DISTINCT CUSTOMER_ID
            FROM CONSECUTIVE
            WHERE PREV_MONTH IS NOT NULL AND PREV_2_MONTH IS NOT NULL;

            示例8 - 分组统计：
            用户：统计每个部门的员工数量和平均工资，只显示人数超过5人的部门
            SELECT D.DEPT_NAME, COUNT(*) AS EMP_COUNT, AVG(E.SALARY) AS AVG_SALARY
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            GROUP BY D.DEPT_NAME
            HAVING COUNT(*) > 5
            ORDER BY EMP_COUNT DESC;
            """,

        DatabaseDialect.MySQL => """
            示例1 - 简单查询：
            用户：查询所有员工的姓名和工资
            SELECT EMP_NAME, SALARY FROM EMPLOYEE;

            示例2 - 多表关联：
            用户：查询每个员工的姓名和部门名称
            SELECT E.EMP_NAME, D.DEPT_NAME
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID;

            示例3 - 子查询：
            用户：查询工资高于平均工资的员工
            SELECT EMP_NAME, SALARY
            FROM EMPLOYEE
            WHERE SALARY > (SELECT AVG(SALARY) FROM EMPLOYEE);

            示例4 - 窗口函数排名：
            用户：查询每个部门工资排名前3的员工
            SELECT DEPT_NAME, EMP_NAME, SALARY
            FROM (
                SELECT D.DEPT_NAME, E.EMP_NAME, E.SALARY,
                       ROW_NUMBER() OVER(PARTITION BY E.DEPT_ID ORDER BY E.SALARY DESC) AS RN
                FROM EMPLOYEE E
                LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            ) T
            WHERE RN <= 3;

            示例5 - 累计求和：
            用户：查询每个月的销售额和累计销售额
            SELECT SALE_MONTH, AMOUNT,
                   SUM(AMOUNT) OVER(ORDER BY SALE_MONTH) AS CUM_AMOUNT
            FROM MONTHLY_SALES;

            示例6 - 同比增长：
            用户：查询每个月的销售额和同比增长率
            SELECT SALE_MONTH, AMOUNT,
                   LAG(AMOUNT, 12) OVER(ORDER BY SALE_MONTH) AS LAST_YEAR_AMOUNT,
                   ROUND((AMOUNT - LAG(AMOUNT, 12) OVER(ORDER BY SALE_MONTH)) /
                         LAG(AMOUNT, 12) OVER(ORDER BY SALE_MONTH) * 100, 2) AS GROWTH_RATE
            FROM MONTHLY_SALES;

            示例7 - CTE + 连续查询：
            用户：查询连续3个月都有订单的客户
            WITH MONTHLY_ORDERS AS (
                SELECT CUSTOMER_ID,
                       DATE_FORMAT(ORDER_DATE, '%Y-%m') AS ORDER_MONTH,
                       COUNT(*) AS ORDER_COUNT
                FROM ORDERS
                GROUP BY CUSTOMER_ID, DATE_FORMAT(ORDER_DATE, '%Y-%m')
            )
            SELECT DISTINCT CUSTOMER_ID
            FROM MONTHLY_ORDERS M1
            WHERE EXISTS (
                SELECT 1 FROM MONTHLY_ORDERS M2
                WHERE M2.CUSTOMER_ID = M1.CUSTOMER_ID
                AND M2.ORDER_MONTH = DATE_FORMAT(DATE_ADD(STR_TO_DATE(CONCAT(M1.ORDER_MONTH, '-01'), '%Y-%m-%d'), INTERVAL 1 MONTH), '%Y-%m')
            )
            AND EXISTS (
                SELECT 1 FROM MONTHLY_ORDERS M3
                WHERE M3.CUSTOMER_ID = M1.CUSTOMER_ID
                AND M3.ORDER_MONTH = DATE_FORMAT(DATE_ADD(STR_TO_DATE(CONCAT(M1.ORDER_MONTH, '-01'), '%Y-%m-%d'), INTERVAL 2 MONTH), '%Y-%m')
            );

            示例8 - 分组统计：
            用户：统计每个部门的员工数量和平均工资，只显示人数超过5人的部门
            SELECT D.DEPT_NAME, COUNT(*) AS EMP_COUNT, AVG(E.SALARY) AS AVG_SALARY
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            GROUP BY D.DEPT_NAME
            HAVING COUNT(*) > 5
            ORDER BY EMP_COUNT DESC;
            """,

        DatabaseDialect.SqlServer => """
            示例1 - 简单查询：
            用户：查询所有员工的姓名和工资
            SELECT EMP_NAME, SALARY FROM EMPLOYEE;

            示例2 - 多表关联：
            用户：查询每个员工的姓名和部门名称
            SELECT E.EMP_NAME, D.DEPT_NAME
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID;

            示例3 - 子查询：
            用户：查询工资高于平均工资的员工
            SELECT EMP_NAME, SALARY
            FROM EMPLOYEE
            WHERE SALARY > (SELECT AVG(SALARY) FROM EMPLOYEE);

            示例4 - 窗口函数排名：
            用户：查询每个部门工资排名前3的员工
            SELECT DEPT_NAME, EMP_NAME, SALARY
            FROM (
                SELECT D.DEPT_NAME, E.EMP_NAME, E.SALARY,
                       ROW_NUMBER() OVER(PARTITION BY E.DEPT_ID ORDER BY E.SALARY DESC) AS RN
                FROM EMPLOYEE E
                LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            ) T
            WHERE RN <= 3;

            示例5 - 累计求和：
            用户：查询每个月的销售额和累计销售额
            SELECT SALE_MONTH, AMOUNT,
                   SUM(AMOUNT) OVER(ORDER BY SALE_MONTH) AS CUM_AMOUNT
            FROM MONTHLY_SALES;

            示例6 - 分组统计：
            用户：统计每个部门的员工数量和平均工资，只显示人数超过5人的部门
            SELECT D.DEPT_NAME, COUNT(*) AS EMP_COUNT, AVG(E.SALARY) AS AVG_SALARY
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            GROUP BY D.DEPT_NAME
            HAVING COUNT(*) > 5
            ORDER BY EMP_COUNT DESC;
            """,

        _ => """
            示例1 - 简单查询：
            用户：查询所有员工的姓名和工资
            SELECT EMP_NAME, SALARY FROM EMPLOYEE;

            示例2 - 多表关联：
            用户：查询每个员工的姓名和部门名称
            SELECT E.EMP_NAME, D.DEPT_NAME
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID;

            示例3 - 子查询：
            用户：查询工资高于平均工资的员工
            SELECT EMP_NAME, SALARY
            FROM EMPLOYEE
            WHERE SALARY > (SELECT AVG(SALARY) FROM EMPLOYEE);

            示例4 - 窗口函数排名：
            用户：查询每个部门工资排名前3的员工
            SELECT DEPT_NAME, EMP_NAME, SALARY
            FROM (
                SELECT D.DEPT_NAME, E.EMP_NAME, E.SALARY,
                       ROW_NUMBER() OVER(PARTITION BY E.DEPT_ID ORDER BY E.SALARY DESC) AS RN
                FROM EMPLOYEE E
                LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            ) T
            WHERE RN <= 3;

            示例5 - 累计求和：
            用户：查询每个月的销售额和累计销售额
            SELECT SALE_MONTH, AMOUNT,
                   SUM(AMOUNT) OVER(ORDER BY SALE_MONTH) AS CUM_AMOUNT
            FROM MONTHLY_SALES;

            示例6 - 分组统计：
            用户：统计每个部门的员工数量和平均工资，只显示人数超过5人的部门
            SELECT D.DEPT_NAME, COUNT(*) AS EMP_COUNT, AVG(E.SALARY) AS AVG_SALARY
            FROM EMPLOYEE E
            LEFT JOIN DEPARTMENT D ON E.DEPT_ID = D.DEPT_ID
            GROUP BY D.DEPT_NAME
            HAVING COUNT(*) > 5
            ORDER BY EMP_COUNT DESC;
            """
    };

    /// <summary>
    /// 单轮生成
    /// </summary>
    public async Task<string> GenerateAsync(string naturalLanguage)
    {
        var result = await _client.ChatAsync(_systemPrompt, naturalLanguage);
        return CleanSql(result);
    }

    /// <summary>
    /// 多轮对话生成（支持追问）
    /// </summary>
    public async Task<string> GenerateWithHistoryAsync(string naturalLanguage)
    {
        _conversationHistory.Add(("user", naturalLanguage));
        var result = await _client.ChatAsync(_systemPrompt, _conversationHistory);
        var sql = CleanSql(result);
        _conversationHistory.Add(("assistant", sql));
        return sql;
    }

    /// <summary>
    /// 清空对话历史
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    /// <summary>
    /// 清理 LLM 返回的 SQL，去除 markdown 代码块等多余内容
    /// </summary>
    private static string CleanSql(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // 去除 ```sql ... ``` 或 ``` ... ``` 代码块包裹
        var match = Regex.Match(text, @"```(?:sql)?\s*\r?\n?(.*?)\r?\n?```", RegexOptions.Singleline);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // 去除开头的 ```sql 或 ```
        text = Regex.Replace(text, @"^```(?:sql)?\s*\r?\n?", "", RegexOptions.Multiline).Trim();
        // 去除结尾的 ```
        text = Regex.Replace(text, @"\r?\n?```\s*$", "", RegexOptions.Multiline).Trim();

        return text;
    }
}
