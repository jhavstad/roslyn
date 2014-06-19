﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Utilities;
using Microsoft.CodeAnalysis.FxCopDiagnosticFixers;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FxCopAnalyzers.Usage
{
    public abstract class CA2235CodeFixProviderBase : MultipleCodeFixProviderBase
    {
        public sealed override IEnumerable<string> GetFixableDiagnosticIds()
        {
            return SpecializedCollections.SingletonEnumerable(SerializationRulesDiagnosticAnalyzer.RuleCA2235Id);
        }

        protected abstract SyntaxNode GetFieldDeclarationNode(SyntaxNode node);

        internal async override Task<IEnumerable<CodeAction>> GetFixesAsync(Document document, SemanticModel model, SyntaxNode root, SyntaxNode nodeToFix, CancellationToken cancellationToken)
        {
            IEnumerable<CodeAction> actions = null;

            // Fix 1: Add a NonSerialized attribute to the field
            var fieldNode = GetFieldDeclarationNode(nodeToFix);
            if (fieldNode != null)
            { 
                var attr = CodeGenerationSymbolFactory.CreateAttributeData(WellKnownTypes.NonSerializedAttribute(model.Compilation));
                var newNode = CodeGenerator.AddAttributes(fieldNode, document.Project.Solution.Workspace, SpecializedCollections.SingletonEnumerable(attr)).WithAdditionalAnnotations(Formatting.Formatter.Annotation);
                var newDocument = document.WithSyntaxRoot(root.ReplaceNode(fieldNode, newNode));
                var codeAction = CodeAction.Create(FxCopFixersResources.AddNonSerializedAttribute, newDocument);
                actions = SpecializedCollections.SingletonEnumerable(codeAction);

                // Fix 2: If the type of the field is defined in source, then add the serializable attribute to the type.
                var fieldSymbol = model.GetDeclaredSymbol(nodeToFix) as IFieldSymbol;
                var type = fieldSymbol.Type;
                if (type.Locations.Any(l => l.IsInSource))
                {
                    var typeDeclNode = type.DeclaringSyntaxReferences.First().GetSyntax();
                    var serializableAttr = CodeGenerationSymbolFactory.CreateAttributeData(WellKnownTypes.SerializableAttribute(model.Compilation));
                    var newTypeDeclNode = CodeGenerator.AddAttributes(typeDeclNode, document.Project.Solution.Workspace, SpecializedCollections.SingletonEnumerable(serializableAttr)).WithAdditionalAnnotations(Formatting.Formatter.Annotation);

                    var documentContainingNode = document.Project.Solution.GetDocument(typeDeclNode.SyntaxTree);
                    var docRoot = await documentContainingNode.GetSyntaxRootAsync(cancellationToken);
                    var newDocumentContainingNode = documentContainingNode.WithSyntaxRoot(docRoot.ReplaceNode(typeDeclNode, newTypeDeclNode));
                    var typeCodeAction = CodeAction.Create(FxCopFixersResources.AddSerializableAttribute, newDocumentContainingNode.Project.Solution);

                    actions = actions.Concat(typeCodeAction);
                }
            }

            return actions;
        }
    }
}