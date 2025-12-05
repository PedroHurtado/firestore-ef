// Test frameworks
global using Xunit;
global using FluentAssertions;
global using Moq;

// EF Core namespaces
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Infrastructure;
global using Microsoft.EntityFrameworkCore.Metadata;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.EntityFrameworkCore.Metadata.Conventions;
global using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
global using Microsoft.EntityFrameworkCore.Storage;
global using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
global using Microsoft.EntityFrameworkCore.Query;
global using Microsoft.EntityFrameworkCore.Update;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.ChangeTracking;

// Firestore provider namespaces
global using Firestore.EntityFrameworkCore.Infrastructure;
global using Firestore.EntityFrameworkCore.Storage;
global using Firestore.EntityFrameworkCore.Query;
global using Firestore.EntityFrameworkCore.Metadata.Conventions;

// Microsoft Extensions
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// System namespaces
global using System.Linq.Expressions;
global using System.Reflection;
global using System.ComponentModel.DataAnnotations;
global using System.ComponentModel.DataAnnotations.Schema;
