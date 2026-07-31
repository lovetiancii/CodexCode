using System.Linq.Expressions;

namespace Tianci.OA.Application.Common;

public static class PredicateExtensions
{
    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplaceVisitor(right.Parameters[0], parameter).Visit(right.Body)
            ?? throw new InvalidOperationException("无法组合查询条件");

        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(left.Body, rightBody),
            parameter);
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }
}

