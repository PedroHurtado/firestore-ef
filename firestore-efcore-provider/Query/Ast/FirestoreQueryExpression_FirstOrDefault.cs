using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de FirstOrDefault.
    /// </summary>
    public record TranslateFirstOrDefaultRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Predicate,
        Type ReturnType,
        bool ReturnDefault);

    /// <summary>
    /// Feature: FirstOrDefault with Id optimization.
    /// When a query is only filtering by Id with ==, we can use GetDocumentAsync
    /// instead of running a full query (more efficient).
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region FirstOrDefault Properties

        /// <summary>
        /// Si la query es solo por ID, contiene la expresión del ID.
        /// En este caso, se usará GetDocumentAsync en lugar de ExecuteQueryAsync.
        /// </summary>
        public Expression? IdValueExpression { get; protected set; }

        /// <summary>
        /// Indica si esta query es solo por ID (sin otros filtros)
        /// </summary>
        public bool IsIdOnlyQuery => IdValueExpression != null;

        /// <summary>
        /// Indica si debe devolver default (null) cuando no hay resultados.
        /// true = FirstOrDefault (devuelve null)
        /// false = First (lanza excepción)
        /// </summary>
        public bool ReturnDefault { get; protected set; }

        /// <summary>
        /// Tipo de retorno de la query (para First/FirstOrDefault).
        /// </summary>
        public Type? ReturnType { get; protected set; }

        #endregion

        #region FirstOrDefault Commands

        /// <summary>
        /// Establece la expresión del ID para queries por documento único.
        /// </summary>
        public FirestoreQueryExpression WithIdValueExpression(Expression idExpression)
        {
            IdValueExpression = idExpression;
            return this;
        }

        /// <summary>
        /// Establece ReturnDefault y ReturnType.
        /// </summary>
        public FirestoreQueryExpression WithReturnDefault(bool returnDefault, Type returnType)
        {
            ReturnDefault = returnDefault;
            ReturnType = returnType;
            return this;
        }

        /// <summary>
        /// Limpia la expresión del ID y establece filtros iniciales.
        /// Usado para convertir una IdOnlyQuery a una query normal con filtros.
        /// </summary>
        public FirestoreQueryExpression ClearIdValueExpressionWithFilters(IEnumerable<FirestoreWhereClause> initialFilters)
        {
            IdValueExpression = null;
            _filters.Clear();
            _filters.AddRange(initialFilters);
            return this;
        }

        #endregion

        #region FirstOrDefault Translation

        /// <summary>
        /// Traduce FirstOrDefault con posible optimización de Id.
        /// Si el predicate es solo Id == value, aplica optimización (GetDocumentAsync).
        /// Si no, usa FirestoreWhereTranslator para traducir el predicate.
        /// </summary>
        public static ShapedQueryExpression? TranslateFirstOrDefault(TranslateFirstOrDefaultRequest request)
        {
            var (source, predicate, returnType, returnDefault) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Almacenar ReturnDefault y ReturnType en el AST
            ast.WithReturnDefault(returnDefault, returnType);

            // Sin predicate: solo aplicar limit
            if (predicate == null)
            {
                ast.WithLimit(1);
                return source.UpdateQueryExpression(ast);
            }

            // Intentar optimización de Id
            var idOptimized = TryApplyIdOptimization(source, predicate);
            if (idOptimized != null)
                return idOptimized;

            // Traducir predicate con FirestoreWhereTranslator
            var translator = new FirestoreWhereTranslator();
            var filterResult = translator.Translate(predicate.Body);

            if (filterResult == null)
                return null;

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

            ast.WithLimit(1);
            return source.UpdateQueryExpression(ast);
        }

        /// <summary>
        /// Intenta aplicar la optimización de Id.
        /// Solo aplica si el predicate es exactamente Id == value sin otros filtros.
        /// </summary>
        private static ShapedQueryExpression? TryApplyIdOptimization(
            ShapedQueryExpression source,
            LambdaExpression predicate)
        {
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // No aplica si ya hay filtros existentes
            if (ast.Filters.Count > 0 || ast.OrFilterGroups.Count > 0)
                return null;

            // No aplica si ya es IdOnlyQuery
            if (ast.IsIdOnlyQuery)
                return null;

            // Traducir el predicate para ver si es Id == value
            var translator = new FirestoreWhereTranslator();
            var filterResult = translator.Translate(predicate.Body);

            if (filterResult == null)
                return null;

            // Solo aplica para un único clause AND (no OR groups)
            if (filterResult.IsOrGroup)
                return null;

            var clauses = filterResult.AndClauses;

            // Solo aplica para un único clause
            if (clauses.Count != 1)
                return null;

            var clause = clauses[0];

            // Solo aplica si es la propiedad Id
            if (clause.PropertyName != "Id")
                return null;

            // Solo soporta operador ==
            if (clause.Operator != FirestoreOperator.EqualTo)
                return null;

            // Aplicar optimización
            ast.WithIdValueExpression(clause.ValueExpression);
            return source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
