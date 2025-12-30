using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de SingleOrDefault.
    /// </summary>
    public record TranslateSingleOrDefaultRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Predicate,
        Type ReturnType,
        bool ReturnDefault);

    /// <summary>
    /// Feature: SingleOrDefault translation.
    /// SingleOrDefault uses Limit 2 to detect duplicates - EF Core throws if more than one result.
    /// No Id optimization because we need to detect duplicates.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region SingleOrDefault Translation

        /// <summary>
        /// Traduce SingleOrDefault.
        /// Usa Limit 2 para detectar duplicados (EF Core lanza excepción si hay más de uno).
        /// No aplica optimización de Id porque necesita detectar duplicados.
        /// </summary>
        public static ShapedQueryExpression? TranslateSingleOrDefault(TranslateSingleOrDefaultRequest request)
        {
            var (source, predicate, returnType, returnDefault) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Almacenar ReturnDefault y ReturnType en el AST
            ast.WithReturnDefault(returnDefault, returnType);

            // Sin predicate: solo aplicar limit 2
            if (predicate == null)
            {
                ast.WithLimit(2);
                return source.UpdateQueryExpression(ast);
            }

            // Traducir predicate con FirestoreWhereTranslator
            var translator = new FirestoreWhereTranslator();
            var filterResult = translator.Translate(predicate.Body);

            if (filterResult == null)
                return null;

            // Store the filter result for later processing
            ast.AddFilterResult(filterResult);

            // Aplicar filtros al AST
            if (filterResult.IsOrGroup)
            {
                ast.AddOrFilterGroup(filterResult.OrGroup!);
            }
            else
            {
                foreach (var clause in filterResult.AndClauses)
                {
                    ast.AddFilter(clause);
                }
            }

            // Limit 2 para detectar duplicados
            ast.WithLimit(2);
            return source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
