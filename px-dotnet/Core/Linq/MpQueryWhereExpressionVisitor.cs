﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Remotion.Linq.Clauses.Expressions;
using Remotion.Linq.Parsing;

namespace MercadoPago.Core.Linq
{
    public class MpQueryWhereExpressionVisitor : ThrowingExpressionVisitor
    {
        private readonly Dictionary<string, string> _queryParameters;

        public static void GetQueryParameters(Dictionary<string, string> queryParameters, Expression linqExpression)
        {
            var visitor = new MpQueryWhereExpressionVisitor(queryParameters);
            visitor.Visit(linqExpression);
        }

        public MpQueryWhereExpressionVisitor(Dictionary<string, string> queryParameters)
        {
            _queryParameters = queryParameters;
        }

        private Type GetEnumType(MemberExpression memberExpression)
        {
            var memberType = ((PropertyInfo) memberExpression.Member).PropertyType;

            if (memberType.IsEnum)
                return memberExpression.Member.ReflectedType;

            if (memberType.IsGenericType && memberType.GetGenericArguments()[0].IsEnum)
                return memberType.GetGenericArguments()[0];

            return null;
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Equal:
                    var left =
                        expression.Left as MemberExpression
                        ?? (expression.Left is UnaryExpression unary
                            ? unary.Operand as MemberExpression
                            : null);

                    if (left == null)
                        throw new NotSupportedException($"Expression: {expression} is not supported.");

                    if (!(expression.Right is ConstantExpression right))
                        throw new NotSupportedException($"Expression: {expression} is not supported.");

                    var key = left.Member.Name.ToSnakeCase();

                    var enumType = GetEnumType(left);

                    var serializableValue =
                        enumType != null
                            ? Enum.ToObject(enumType,right.Value)
                            : right.Value;

                    var value = Serialization.SerializeValue(serializableValue);

                    if (!string.IsNullOrEmpty(value))
                        _queryParameters.Add(key, value);
                    break;
                case ExpressionType.AndAlso:
                case ExpressionType.And:
                    Visit(expression.Left);
                    Visit(expression.Right);
                    break;
                default:
                    throw new NotSupportedException($"Expression: {expression} is not supported.");
            }

            return expression;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            // In production code, handle this via method lookup tables.

            if (expression.Method.Name == "Contains")
            {
                Visit(expression.Object);
                ////_expression.Append(".indexOf(");
                Visit(expression.Arguments[0]);
                ////_expression.Append(") != -1");
                return expression;
            }

            if (expression.Method.Name == "StartsWith")
            {
                Visit(expression.Object);
                ////_expression.Append(".indexOf(");
                Visit(expression.Arguments[0]);
                ////_expression.Append(") == 0");
                return expression;
            }

            if (expression.Method.Name == "EndsWith")
            {
                Visit(expression.Object);
                ////_expression.Append(".indexOf(");
                Visit(expression.Arguments[0]);
                ////_expression.Append(", ");
                Visit(expression.Object);
                ////_expression.Append(".length - '");
                Visit(expression.Arguments[0]);
                ////_expression.Append("'.length) != -1");
                return expression;
            }

            if (expression.Method.Name == "ToLower")
            {
                Visit(expression.Object);
                ////_expression.Append(".toLowerCase()");
                return expression;
            }
            if (expression.Method.Name == "ToUpper")
            {
                Visit(expression.Object);
                ////_expression.Append(".toUpperCase()");
                return expression;
            }

            return base.VisitMethodCall(expression); // throws
        }

        // Called when a LINQ expression type is not handled above.
        protected override Exception CreateUnhandledItemException<T>(T unhandledItem, string visitMethod)
        {
            string itemText = FormatUnhandledItem(unhandledItem);
            var message = $"The expression '{itemText}' (type: {typeof(T)}) is not supported by this LINQ provider.";
            return new NotSupportedException(message);
        }

        private static string FormatUnhandledItem<T>(T unhandledItem)
        {
            var itemAsExpression = unhandledItem as Expression;
            return itemAsExpression?.ToString() ?? unhandledItem.ToString();
        }
    }
}