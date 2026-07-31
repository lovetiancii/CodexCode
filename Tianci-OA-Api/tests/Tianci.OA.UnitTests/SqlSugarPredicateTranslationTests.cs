using System.Linq.Expressions;
using SqlSugar;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Employees;

namespace Tianci.OA.UnitTests;

public sealed class SqlSugarPredicateTranslationTests
{
    [Fact]
    public void Optional_department_filter_generates_valid_boolean_sql()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Server=localhost;Database=translation_test;User=test;Password=test;",
            DbType = DbType.MySql,
            InitKeyType = InitKeyType.Attribute,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityNameService = (_, info) => info.DbTableName = "employee",
                EntityService = (property, column) => column.DbColumnName = UtilMethods.ToUnderLine(property.Name)
            }
        });
        Expression<Func<Employee, bool>> predicate = employee => !employee.IsDeleted;
        const long departmentId = 42;
        predicate = predicate.And(employee => employee.DepartmentId == departmentId);

        var sql = db.Queryable<Employee>().Where(predicate).ToSql().Key;

        Assert.Contains("department_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IS NOT NULL )(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IS NULL ))", sql, StringComparison.OrdinalIgnoreCase);
    }
}

