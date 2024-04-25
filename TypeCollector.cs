using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeContextCollector
{
    internal class TypeCollector : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;

        public TypeCollector(SemanticModel semanticModel)
        {
            _semanticModel = semanticModel;
        }

        public HashSet<INamedTypeSymbol> TypeSymbols { get; } = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol is INamedTypeSymbol symbol && symbol.Locations.Any(loc => loc.IsInSource))
            {
                TypeSymbols.Add(symbol);
            }

            base.VisitIdentifierName(node);
        }
    }
}