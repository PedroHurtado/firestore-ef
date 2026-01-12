using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
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
        private DbContextOptionsExtensionInfo? _info;
        private string? _projectId;
        private string? _credentialsPath;
        private string? _emulatorHost;
        private string _databaseId;
        private FirestorePipelineOptions _pipelineOptions;

        public FirestoreOptionsExtension()
        {
            _databaseId = "(default)";
            _pipelineOptions = new FirestorePipelineOptions();
        }

        /// <summary>
        /// Constructor de copia - usado internamente para clonar la extensión
        /// </summary>
        protected FirestoreOptionsExtension(FirestoreOptionsExtension copyFrom)
        {
            _projectId = copyFrom._projectId;
            _credentialsPath = copyFrom._credentialsPath;
            _emulatorHost = copyFrom._emulatorHost;
            _databaseId = copyFrom._databaseId;
            _pipelineOptions = copyFrom._pipelineOptions;
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
        /// Host del emulador de Firestore (ej: "127.0.0.1:8080").
        /// Si está configurado, se establece FIRESTORE_EMULATOR_HOST automáticamente.
        /// </summary>
        public virtual string? EmulatorHost => _emulatorHost;

        /// <summary>
        /// Opciones de configuración del pipeline de queries.
        /// </summary>
        public virtual FirestorePipelineOptions PipelineOptions => _pipelineOptions;

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
        /// Crea una nueva extensión con el host del emulador especificado.
        /// Configura automáticamente la variable de entorno FIRESTORE_EMULATOR_HOST.
        /// </summary>
        public virtual FirestoreOptionsExtension WithEmulatorHost(string? emulatorHost)
        {
            var clone = Clone();
            clone._emulatorHost = emulatorHost;

            // Configurar la variable de entorno automáticamente
            if (!string.IsNullOrEmpty(emulatorHost))
            {
                Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", emulatorHost);
            }

            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con el nivel de logging de queries especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithQueryLogLevel(QueryLogLevel queryLogLevel)
        {
            var clone = Clone();
            clone._pipelineOptions = new FirestorePipelineOptions
            {
                QueryLogLevel = queryLogLevel,
                EnableAstLogging = _pipelineOptions.EnableAstLogging,
                EnableCaching = _pipelineOptions.EnableCaching,
                MaxRetries = _pipelineOptions.MaxRetries,
                RetryInitialDelay = _pipelineOptions.RetryInitialDelay
            };
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con logging de AST habilitado/deshabilitado
        /// </summary>
        public virtual FirestoreOptionsExtension WithEnableAstLogging(bool enable)
        {
            var clone = Clone();
            clone._pipelineOptions = new FirestorePipelineOptions
            {
                QueryLogLevel = _pipelineOptions.QueryLogLevel,
                EnableAstLogging = enable,
                EnableCaching = _pipelineOptions.EnableCaching,
                MaxRetries = _pipelineOptions.MaxRetries,
                RetryInitialDelay = _pipelineOptions.RetryInitialDelay
            };
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con caching habilitado/deshabilitado
        /// </summary>
        public virtual FirestoreOptionsExtension WithEnableCaching(bool enable)
        {
            var clone = Clone();
            clone._pipelineOptions = new FirestorePipelineOptions
            {
                QueryLogLevel = _pipelineOptions.QueryLogLevel,
                EnableAstLogging = _pipelineOptions.EnableAstLogging,
                EnableCaching = enable,
                MaxRetries = _pipelineOptions.MaxRetries,
                RetryInitialDelay = _pipelineOptions.RetryInitialDelay
            };
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con el número de reintentos del pipeline especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithPipelineMaxRetries(int maxRetries)
        {
            var clone = Clone();
            clone._pipelineOptions = new FirestorePipelineOptions
            {
                QueryLogLevel = _pipelineOptions.QueryLogLevel,
                EnableAstLogging = _pipelineOptions.EnableAstLogging,
                EnableCaching = _pipelineOptions.EnableCaching,
                MaxRetries = maxRetries,
                RetryInitialDelay = _pipelineOptions.RetryInitialDelay
            };
            return clone;
        }

        /// <summary>
        /// Crea una nueva extensión con el delay inicial de reintento del pipeline especificado
        /// </summary>
        public virtual FirestoreOptionsExtension WithPipelineRetryInitialDelay(TimeSpan delay)
        {
            var clone = Clone();
            clone._pipelineOptions = new FirestorePipelineOptions
            {
                QueryLogLevel = _pipelineOptions.QueryLogLevel,
                EnableAstLogging = _pipelineOptions.EnableAstLogging,
                EnableCaching = _pipelineOptions.EnableCaching,
                MaxRetries = _pipelineOptions.MaxRetries,
                RetryInitialDelay = delay
            };
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

            if (_pipelineOptions.MaxRetries < 0)
            {
                throw new InvalidOperationException(
                    "MaxRetries debe ser mayor o igual a 0.");
            }

            if (_pipelineOptions.RetryInitialDelay < TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "RetryInitialDelay debe ser mayor o igual a cero.");
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

                        if (Extension.PipelineOptions.QueryLogLevel != QueryLogLevel.None)
                        {
                            builder.Append(" QueryLogLevel=")
                                   .Append(Extension.PipelineOptions.QueryLogLevel);
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

                debugInfo["Firestore:QueryLogLevel"] =
                    Extension.PipelineOptions.QueryLogLevel.ToString();

                debugInfo["Firestore:MaxRetries"] =
                    Extension.PipelineOptions.MaxRetries.ToString(CultureInfo.InvariantCulture);
            }
        }

        #endregion
    }
}