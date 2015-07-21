// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XUnitConverter
{
    public sealed class AssertArgumentTrimmer : ConverterBase
    {
        private readonly AssertRewriter _rewriter = new AssertRewriter();

        protected override Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var newNode = _rewriter.Visit(syntaxNode);
            if (newNode != syntaxNode)
            {
                document = document.WithSyntaxRoot(newNode);
            }

            return Task.FromResult(document.Project.Solution);
        }

        internal sealed class AssertRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax syntaxNode)
            {
                string assertType = CheckAssert(syntaxNode);
                if (assertType != null)
                {
                    string firstArg = null, secondArg = null, fmt = null;
                    var expr = syntaxNode.Expression as InvocationExpressionSyntax;
                    if (expr.ArgumentList.Arguments.Count != 0)
                    {
                        var firstArgNode = expr.ArgumentList.Arguments.First().Expression;

                        if (assertType == "Equal" || assertType == "NotEqual")
                        {
                            fmt = (assertType == "Equal") ? AssertEqual : AssertNotEqual;
                            firstArg = firstArgNode.ToString();
                            secondArg = expr.ArgumentList.Arguments[1].ToString();
                        }
                        else if (assertType == "Throws")
                        {
                            var throwsExpression = expr.Expression as MemberAccessExpressionSyntax;

                            fmt = AssertThrows;
                            firstArg = throwsExpression.Name.ToString(); // This will give us "Throws<Exception>"
                            secondArg = firstArgNode.ToString();
                        }

                        return SyntaxFactory.ParseStatement(syntaxNode.GetLeadingTrivia().ToFullString() +
                        string.Format(fmt, firstArg, secondArg) +
                        syntaxNode.GetTrailingTrivia().ToFullString());
                    }
                }
                return base.VisitExpressionStatement(syntaxNode);
            }

            public const string AssertEqual = "Assert.Equal({0}, {1});";
            public const string AssertThrows = "Assert.{0}({1});";
            public const string AssertNotEqual = "Assert.NotEqual({0}, {1});";

            public string CheckAssert(SyntaxNode node)
            {
                if (node != null && node.IsKind(SyntaxKind.ExpressionStatement))
                {
                    var invoke = (node as ExpressionStatementSyntax).Expression as InvocationExpressionSyntax;
                    if (invoke != null)
                    {
                        var expr = invoke.Expression as MemberAccessExpressionSyntax;
                        if (!(expr == null || expr.Name == null || expr.Expression == null))
                        {
                            var id = expr.Name.Identifier.ToString().Trim();
                            var caller = expr.Expression.ToString().Trim();

                            if (caller == "Assert")
                            {
                                // Assert.True and Assert.False have already been handled by 
                                // TestAssertTrueOrFalseConverter
                                if ((id != "True") || (id != "False"))
                                    return id;
                            }
                        }
                    }
                }
                return null;
            }
        }
    }
}
