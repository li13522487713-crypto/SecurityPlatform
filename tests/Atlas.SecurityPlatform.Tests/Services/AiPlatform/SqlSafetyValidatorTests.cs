using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Infrastructure.Services.DatabaseStructure;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class SqlSafetyValidatorTests
{
    private readonly SqlSafetyValidator _validator = new();

    [Theory]
    [InlineData("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT);")]
    [InlineData(" create table demo_users (id bigint not null, name varchar(100)) ")]
    [InlineData("CREATE TABLE [order] ([drop] TEXT, `delete` TEXT, \"update\" TEXT);")]
    public void ValidateCreateTable_AllowsSingleCreateTable(string sql)
    {
        _validator.ValidateCreateTable(sql);
    }

    [Theory]
    [InlineData("CREATE TABLE users (id INTEGER); DROP TABLE roles;")]
    [InlineData("CREATE TABLE users (id INTEGER); DELETE FROM audit_log")]
    [InlineData("ALTER TABLE users ADD COLUMN name TEXT")]
    [InlineData("INSERT INTO users(id) VALUES(1)")]
    public void ValidateCreateTable_BlocksDangerousOrMultipleStatements(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateCreateTable(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("WITH x AS (SELECT 1 AS id) SELECT * FROM x")]
    [InlineData("SELECT ';' AS semicolon_text")]
    [InlineData("SELECT \"semi;colon\" AS quoted_text")]
    [InlineData("SELECT `semi;colon` FROM users")]
    [InlineData("SELECT [semi;colon] FROM users")]
    public void ValidateSelectOnly_AllowsReadOnlyQueries(string sql)
    {
        _validator.ValidateSelectOnly(sql);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE users")]
    [InlineData("SELECT * FROM users; UPDATE users SET name = 'x'")]
    [InlineData("SELECT ';'; DELETE FROM users")]
    [InlineData("SELECT \"ignored;\"; INSERT INTO users(id) VALUES(1)")]
    [InlineData("SELECT `ignored;`; ALTER TABLE users ADD name TEXT")]
    [InlineData("SELECT [ignored;]; TRUNCATE TABLE users")]
    [InlineData("DELETE FROM users")]
    [InlineData("/* comment */ TRUNCATE TABLE users")]
    public void ValidateSelectOnly_BlocksMutationAndMultiStatement(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateSelectOnly(sql));
    }

    [Theory]
    [InlineData("CREATE VIEW v_users AS SELECT id, name FROM users")]
    [InlineData("CREATE OR REPLACE VIEW v_users AS SELECT id, name FROM users")]
    public void ValidateCreateView_AllowsCreateView(string sql)
    {
        _validator.ValidateCreateView(sql);
    }

    [Theory]
    [InlineData("CREATE VIEW v_users AS DELETE FROM users")]
    [InlineData("CREATE VIEW v_users AS SELECT * FROM users; DROP VIEW v_users")]
    [InlineData("CREATE DATABASE app_db")]
    public void ValidateCreateView_BlocksDangerousInput(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateCreateView(sql));
    }

    [Theory]
    [InlineData("DROP TABLE users")]
    [InlineData("UPDATE users SET name = 'x'")]
    [InlineData("SELECT LOAD_FILE('/etc/passwd')")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    [InlineData("EXECUTE xp_cmdshell 'dir'")]
    [InlineData("/*! DROP TABLE users */ SELECT 1")]
    [InlineData("")]
    [InlineData(";")]
    public void Validator_BlocksExplicitRiskCases(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateSelectOnly(sql));
    }

    [Theory]
    [InlineData("SELECT 1 FROM users; DROP TABLE users")]
    [InlineData("SELECT 1 FROM users; DELETE FROM users")]
    [InlineData("SELECT 1 FROM users; UPDATE users SET name = 'x'")]
    [InlineData("SELECT 1 FROM users; INSERT INTO users(id) VALUES(1)")]
    [InlineData("SELECT 1 FROM users; ALTER TABLE users ADD name TEXT")]
    [InlineData("SELECT 1 FROM users; TRUNCATE TABLE users")]
    [InlineData("SELECT 1 FROM users; EXEC rebuild_indexes")]
    [InlineData("SELECT 1 FROM users; EXECUTE rebuild_indexes")]
    [InlineData("SELECT 1 FROM users; MERGE INTO users USING src ON users.id = src.id WHEN MATCHED THEN UPDATE SET name = src.name")]
    [InlineData("SELECT 1 FROM users; GRANT SELECT ON users TO app")]
    [InlineData("SELECT 1 FROM users; REVOKE SELECT ON users FROM app")]
    [InlineData("SELECT 1 FROM users; ATTACH DATABASE 'evil.db' AS evil")]
    [InlineData("SELECT 1 FROM users; DETACH DATABASE evil")]
    [InlineData("SELECT 1 FROM users; CREATE USER attacker")]
    [InlineData("SELECT 1 FROM users; CREATE DATABASE attacker")]
    [InlineData("SELECT LOAD_FILE('/etc/passwd')")]
    [InlineData("SELECT * FROM users INTO OUTFILE '/tmp/users.txt'")]
    [InlineData("COPY users TO PROGRAM 'id'")]
    [InlineData("SELECT xp_cmdshell('dir')")]
    public void ValidateSelectOnly_BlocksFullDangerousKeywordAndPhraseSet(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateSelectOnly(sql));
    }

    [Theory]
    [InlineData("SELECT DROP FROM audit_log")]
    [InlineData("SELECT DELETE FROM audit_log")]
    [InlineData("SELECT UPDATE FROM audit_log")]
    [InlineData("SELECT INSERT FROM audit_log")]
    [InlineData("SELECT ALTER FROM audit_log")]
    [InlineData("SELECT TRUNCATE FROM audit_log")]
    [InlineData("SELECT EXEC FROM audit_log")]
    [InlineData("SELECT EXECUTE FROM audit_log")]
    [InlineData("SELECT MERGE FROM audit_log")]
    [InlineData("SELECT GRANT FROM audit_log")]
    [InlineData("SELECT REVOKE FROM audit_log")]
    [InlineData("SELECT ATTACH FROM audit_log")]
    [InlineData("SELECT DETACH FROM audit_log")]
    [InlineData("SELECT CREATE USER FROM audit_log")]
    [InlineData("SELECT CREATE DATABASE FROM audit_log")]
    [InlineData("SELECT LOAD_FILE('/etc/passwd')")]
    [InlineData("SELECT * FROM users INTO OUTFILE '/tmp/users.txt'")]
    [InlineData("COPY users TO PROGRAM 'id'")]
    [InlineData("SELECT xp_cmdshell('dir')")]
    public void ContainsForbiddenKeyword_CoversDangerousKeywordAndPhraseSet(string sql)
    {
        Assert.True(_validator.ContainsForbiddenKeyword(sql));
    }

    [Theory]
    [InlineData("SELECT 'DROP TABLE x' AS text")]
    [InlineData("SELECT \"DROP TABLE x\" AS text")]
    [InlineData("SELECT `DROP TABLE x` FROM users")]
    [InlineData("SELECT [DROP TABLE x] FROM users")]
    [InlineData("SELECT 1 -- DROP TABLE x")]
    [InlineData("SELECT 1 /* DROP TABLE x */")]
    public void ContainsForbiddenKeyword_IgnoresLiteralsIdentifiersAndComments(string sql)
    {
        Assert.False(_validator.ContainsForbiddenKeyword(sql));
    }

    [Theory]
    [InlineData("SELECT 'DROP TABLE x' AS text")]
    [InlineData("SELECT 'a;DROP TABLE x' AS text")]
    [InlineData("SELECT 'it''s; fine' AS text")]
    [InlineData("SELECT \"DROP TABLE x;\" AS text")]
    [InlineData("SELECT `DROP TABLE x;` FROM users")]
    [InlineData("SELECT [DROP TABLE x;] FROM users")]
    [InlineData("SELECT 1 -- DROP TABLE x")]
    [InlineData("SELECT 1 -- DROP TABLE x\r\n")]
    [InlineData("SELECT 1 -- DROP TABLE x\r\n WHERE 1 = 1")]
    [InlineData("CREATE TABLE users(id int) /* DROP TABLE x */")]
    [InlineData("CREATE TABLE users(id int /* DROP TABLE x */)")]
    [InlineData("CREATE TABLE users(id int);")]
    public void Validator_IgnoresLiteralsCommentsAndTrailingSemicolon(string sql)
    {
        if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            _validator.ValidateSelectOnly(sql);
        }
        else
        {
            _validator.ValidateCreateTable(sql);
        }
    }

    [Theory]
    [InlineData("SELECT 'unterminated")]
    [InlineData("SELECT \"unterminated")]
    [InlineData("SELECT `unterminated")]
    [InlineData("SELECT [unterminated")]
    [InlineData("SELECT 1 /* unterminated")]
    [InlineData("/*!50000 SELECT 1 */")]
    public void Validator_BlocksUnclosedQuotingAndExecutableComments(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateSelectOnly(sql));
    }

    [Fact]
    public void SplitStatementsSafely_OnlySplitsSemicolonInExecutableText()
    {
        var statements = _validator.SplitStatementsSafely(
            "SELECT ';' AS a, \"x;y\" AS b, `c;d`, [e;f] FROM users; SELECT 2 -- ; ignored");

        Assert.Equal(2, statements.Count);
        Assert.StartsWith("SELECT", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT 2", statements[1], StringComparison.OrdinalIgnoreCase);
    }
}
