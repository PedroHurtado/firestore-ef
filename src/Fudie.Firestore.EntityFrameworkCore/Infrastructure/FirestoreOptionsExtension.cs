using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure
{
    /// <summary>
    /// Extensión de opciones para el proveedor de Firestore.
    /// Esta clase encapsula toda la configuración necesaria para conectar con Firestore.
    /// </summary>
    public class FirestoreOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo? _info;  // Error 1 y 4: Hacer nullable
        private string? _projectId;  // Error 2: Hacer nullable
        private string? _credentialsPath;  // Error 3: Hacer nullable
        private string _databaseId;
        private int _maxRetryAttempts;
        private TimeSpan _commandTimeout;

        public FirestoreOptionsExtension()
        {
            // Valores por defecto
            _databaseId = "(default)";
            _maxRetryAttempts = 3;
            _commandTimeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Constructor de copia - usado internamente para clonar la extensión
        /// </summary>
        protected FirestoreOptionsExtension(FirestoreOptionsExtension copyFrom)
        {
            _projectId = copyFrom._projectId;
            _credentialsPath = copyFrom._credentialsPath;
            _databaseId = copyFrom._databaseId;
            _maxRetryAttempts = copyFrom._maxRetryAttempts;
            _commandTimeout = copyFrom._commandTimeout;
        }

        #region Propiedades Públicas

        /// <summary>
        /// ID del proyecto de Google Cloud
        /// </summary>
        public virtual string? ProjectId => _projectId;

        /// <summary>
        /// Ruta al archivo de credenciales JSON de Google Cloud
        /// </summary>
        public virtual string? CredentialsPath => _credentialsPath;

        /// <summary>
        /// ID de la base de datos de Firestore (por defecto es "(default)")
        /// </summary>
        public virtual string DatabaseId => _databaseId;

        /// <summary>
        /// Número máximo de reintentos para operaciones fallidas
        /// </summary>
        public virtual int MaxRetryAttempts => _maxRetryAttempts;

        /// <summary>
        /// Timeout para comandos de Firestore
        /// </summary>
        public virtual TimeSpan CommandTimeout => _commandTimeout;

        #endregion

        #region Métodos With (Patrón Immutable)

        /// <summary>
        /// Crea una nueva extensión con el ProjectId especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithProjectId(string projectId)
        {
            var clone = Clone();
            clone._projectId = projectId;
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con la ruta de credenciales especificada
        /// </summary>
        public virtual FirestoreOptionsExtension WithCredentialsPath(string? credentialsPath)
        {
            var clone = Clone();
            clone._credentialsPath = credentialsPath;
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con el DatabaseId especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithDatabaseId(string? databaseId)
        {
            var clone = Clone();
            clone._databaseId = databaseId ?? "(default)";
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con el número de reintentos especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithMaxRetryAttempts(int maxRetryAttempts)
        {
            var clone = Clone();
            clone._maxRetryAttempts = maxRetryAttempts;
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con el timeout especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithCommandTimeout(TimeSpan commandTimeout)
        {
            var clone = Clone();
            clone._commandTimeout = commandTimeout;
            return clone;
        }

        #endregion

        #region IDbContextOptionsExtension Implementation

        /// <summary>
        /// Información sobre esta extensión (para logging y diagnóstico)
        /// </summary>
        public virtual DbContextOptionsExtensionInfo Info
            => _info ??= new ExtensionInfo(this);

        /// <summary>
        /// Clona la extensión (patrón immutable)
        /// </summary>
        protected virtual FirestoreOptionsExtension Clone()
            => new(this);

        /// <summary>
        /// Registra los servicios necesarios para el proveedor de Firestore
        /// </summary>
        public void ApplyServices(IServiceCollection services)
        {
            // Aquí se registrarán todos los servicios del proveedor
            services.AddEntityFrameworkFirestore();
        }

        /// <summary>
        /// Valida que la configuración sea correcta
        /// </summary>
        public void Validate(IDbContextOptions options)
        {
            if (string.IsNullOrWhiteSpace(_projectId))
            {
                throw new InvalidOperationException(
                    "El ProjectId es requerido para usar Firestore. " +
                    "Configúralo usando optionsBuilder.UseFirestore(projectId: \"tu-proyecto\")");
            }

            if (_maxRetryAttempts < 0)
            {
                throw new InvalidOperationException(
                    "MaxRetryAttempts debe ser mayor o igual a 0.");
            }

            if (_commandTimeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "CommandTimeout debe ser mayor que cero.");
            }
        }

        #endregion

        #region ExtensionInfo (Clase Interna)

        /// <summary>
        /// Proporciona información sobre la extensión para logging y caching
        /// </summary>
        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            private string? _logFragment;  // Error 5: Hacer nullable
            private int? _serviceProviderHash;

            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension)
            {
            }

            private new FirestoreOptionsExtension Extension
                => (FirestoreOptionsExtension)base.Extension;

            /// <summary>
            /// Indica que esta es una extensión de proveedor de base de datos
            /// </summary>
            public override bool IsDatabaseProvider => true;

            /// <summary>
            /// Fragmento de log que describe la configuración
            /// </summary>
            public override string LogFragment
            {
                get
                {
                    if (_logFragment == null)
                    {
                        var builder = new System.Text.StringBuilder();
                        
                        builder.Append("ProjectId=").Append(Extension.ProjectId ?? "null");
                        
                        if (!string.IsNullOrEmpty(Extension.DatabaseId) && 
                            Extension.DatabaseId != "(default)")
                        {
                            builder.Append(" DatabaseId=").Append(Extension.DatabaseId);
                        }

                        if (Extension.MaxRetryAttempts != 3)
                        {
                            builder.Append(" MaxRetryAttempts=")
                                   .Append(Extension.MaxRetryAttempts);
                        }

                        _logFragment = builder.ToString();
                    }

                    return _logFragment;
                }
            }

            /// <summary>
            /// Hash para determinar si dos configuraciones son iguales
            /// (para compartir el ServiceProvider)
            /// </summary>
            public override int GetServiceProviderHashCode()
            {
                if (_serviceProviderHash == null)
                {
                    var hashCode = new HashCode();
                    hashCode.Add(Extension.ProjectId);
                    hashCode.Add(Extension.DatabaseId);
                    hashCode.Add(Extension.CredentialsPath);
                    _serviceProviderHash = hashCode.ToHashCode();
                }

                return _serviceProviderHash.Value;
            }

            /// <summary>
            /// Determina si dos extensiones deben usar el mismo ServiceProvider
            /// </summary>
            public override bool ShouldUseSameServiceProvider(
                DbContextOptionsExtensionInfo other)
            {
                return other is ExtensionInfo otherInfo
                    && Extension.ProjectId == otherInfo.Extension.ProjectId
                    && Extension.DatabaseId == otherInfo.Extension.DatabaseId
                    && Extension.CredentialsPath == otherInfo.Extension.CredentialsPath;
            }

            /// <summary>
            /// Llena información de debug para diagnóstico
            /// </summary>
            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["Firestore:ProjectId"] = 
                    Extension.ProjectId?.GetHashCode().ToString(CultureInfo.InvariantCulture) 
                    ?? "null";
                
                debugInfo["Firestore:DatabaseId"] = Extension.DatabaseId;
                
                debugInfo["Firestore:MaxRetryAttempts"] = 
                    Extension.MaxRetryAttempts.ToString(CultureInfo.InvariantCulture);
                
                debugInfo["Firestore:CommandTimeout"] = 
                    Extension.CommandTimeout.ToString();
            }
        }

        #endregion
    }
}