using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Infrastructure.Services.DatabaseStructure;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

public sealed class SqlSafetyValidatorTests
{
    private readonly SqlSafetyValidator _validator = new();

    [Theory]
    [InlineData("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT);")]
    [InlineData(" create table demo_users (id bigint not null, name varchar(100)) ")]
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
    public void ValidateSelectOnly_AllowsReadOnlyQueries(string sql)
    {
        _validator.ValidateSelectOnly(sql);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE users")]
    [InlineData("SELECT * FROM users; UPDATE users SET name = 'x'")]
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
    [InlineData("/*! DROP TABLE users */ SELECT 1")]
    [InlineData("")]
    [InlineData(";")]
    public void Validator_BlocksExplicitRiskCases(string sql)
    {
        Assert.Throws<SqlSafetyException>(() => _validator.ValidateSelectOnly(sql));
    }

    [Theory]
    [InlineData("SELECT 'DROP TABLE x' AS text")]
    [InlineData("SELECT 1 -- DROP TABLE x")]
    [InlineData("CREATE TABLE users(id int) /* DROP TABLE x */")]
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
}
