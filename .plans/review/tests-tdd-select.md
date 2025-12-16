# Tests TDD: Select (Proyecciones) - EF Core Firestore Provider

**Fecha:** 2025-12-15

---

## Fase 1: Campos Simples

### Ciclo 1: Select campo único

**Test:** `Select_CampoUnico_RetornaSoloCampo`

```
Arrange: Crear productos con Id, Nombre, Precio, Descripcion
Act:     Select(p => p.Nombre)
Assert:  Retorna solo los nombres, sin otros campos
```

---

### Ciclo 2: Select múltiples campos (tipo anónimo)

**Test:** `Select_TipoAnonimo_RetornaSoloCamposSeleccionados`

```
Arrange: Crear productos con Id, Nombre, Precio, Descripcion, PrecioCoste
Act:     Select(p => new { p.Id, p.Nombre, p.Precio })
Assert:  Retorna objetos con solo Id, Nombre, Precio
         PrecioCoste NO debe viajar (verificar en query Firestore)
```

---

### Ciclo 3: Select a DTO

**Test:** `Select_Dto_MapeaCamposCorrectamente`

```
Arrange: Crear productos con todos los campos
Act:     Select(p => new ProductoReadDto { Id = p.Id, Nombre = p.Nombre })
Assert:  Retorna DTOs con campos mapeados correctamente
```

---

## Fase 2: ComplexTypes

### Ciclo 4: Select ComplexType completo

**Test:** `Select_ComplexTypeCompleto_RetornaObjetoAnidado`

```
Arrange: Crear empresas con DireccionFiscal (Calle, Ciudad, CP)
Act:     Select(e => e.DireccionFiscal)
Assert:  Retorna el ComplexType completo con todas sus propiedades
```

---

### Ciclo 5: Select campo de ComplexType

**Test:** `Select_CampoDeComplexType_RetornaSoloCampo`

```
Arrange: Crear empresas con DireccionFiscal (Calle, Ciudad, CP)
Act:     Select(e => e.DireccionFiscal.Ciudad)
Assert:  Retorna solo la ciudad, no el ComplexType completo
```

---

### Ciclo 6: Select múltiples campos de ComplexType

**Test:** `Select_MultiplesCamposComplexType_RetornaSoloSeleccionados`

```
Arrange: Crear empresas con DireccionFiscal (Calle, Ciudad, CP)
Act:     Select(e => new { e.Nombre, e.DireccionFiscal.Ciudad, e.DireccionFiscal.CP })
Assert:  Retorna nombre de empresa + ciudad + CP, sin Calle
```

---

## Fase 3: Combinación Where + Select

### Ciclo 7: Where + Select campos

**Test:** `Where_Select_FiltraYProyecta`

```
Arrange: Crear productos activos e inactivos con varios campos
Act:     Where(p => p.Activo).Select(p => new { p.Id, p.Nombre })
Assert:  Retorna solo activos, solo con Id y Nombre
```

---

### Ciclo 8: Where + Select a DTO

**Test:** `Where_SelectDto_FiltraYMapea`

```
Arrange: Crear productos de diferentes categorías
Act:     Where(p => p.Categoria == "Electrónica")
         .Select(p => new ProductoListaDto { Id = p.Id, Nombre = p.Nombre })
Assert:  Retorna DTOs solo de categoría Electrónica
```

---

### Ciclo 9: Where + OrderBy + Select

**Test:** `Where_OrderBy_Select_FiltraOrdenaProyecta`

```
Arrange: Crear productos con diferentes precios y categorías
Act:     Where(p => p.Categoria == "Electrónica")
         .OrderByDescending(p => p.Precio)
         .Select(p => new { p.Nombre, p.Precio })
Assert:  Retorna filtrados, ordenados por precio desc, solo campos seleccionados
```

---

### Ciclo 10: Where + OrderBy + Take + Select

**Test:** `Where_OrderBy_Take_Select_Top3MasCaros`

```
Arrange: Crear 10 productos de categoría Electrónica
Act:     Where(p => p.Categoria == "Electrónica")
         .OrderByDescending(p => p.Precio)
         .Take(3)
         .Select(p => new { p.Nombre, p.Precio })
Assert:  Retorna exactamente 3, los más caros, solo con Nombre y Precio
```

---

## Fase 4: Subcollections en Select

### Ciclo 11: Select con subcollection completa

**Test:** `Select_SubcollectionCompleta_CargaTodos`

```
Arrange: Crear cliente con 5 pedidos
Act:     Select(c => new { c.Nombre, c.Pedidos })
Assert:  Retorna cliente con sus 5 pedidos completos
```

---

### Ciclo 12: Select con subcollection proyectada

**Test:** `Select_SubcollectionProyectada_SoloCamposSeleccionados`

```
Arrange: Crear cliente con pedidos (Id, Fecha, Total, Estado, Notas)
Act:     Select(c => new { 
             c.Nombre, 
             Pedidos = c.Pedidos.Select(p => new { p.Id, p.Total }) 
         })
Assert:  Retorna cliente con pedidos que solo tienen Id y Total
```

---

### Ciclo 13: Select con subcollection filtrada

**Test:** `Select_SubcollectionFiltrada_SoloCoincidencias`

```
Arrange: Crear cliente con pedidos en diferentes estados
Act:     Select(c => new { 
             c.Nombre, 
             PedidosPendientes = c.Pedidos.Where(p => p.Estado == "Pendiente") 
         })
Assert:  Retorna cliente solo con pedidos pendientes
```

---

### Ciclo 14: Select con subcollection filtrada + ordenada

**Test:** `Select_SubcollectionFiltradaOrdenada_OrdenCorrecto`

```
Arrange: Crear cliente con pedidos pendientes de diferentes fechas
Act:     Select(c => new { 
             c.Nombre, 
             PedidosPendientes = c.Pedidos
                 .Where(p => p.Estado == "Pendiente")
                 .OrderByDescending(p => p.Fecha)
         })
Assert:  Retorna cliente con pedidos pendientes ordenados por fecha desc
```

---

### Ciclo 15: Select con subcollection filtrada + ordenada + limitada

**Test:** `Select_SubcollectionCompleta_FiltroOrdenLimite`

```
Arrange: Crear cliente con 10 pedidos pendientes
Act:     Select(c => new { 
             c.Nombre, 
             UltimosPendientes = c.Pedidos
                 .Where(p => p.Estado == "Pendiente")
                 .OrderByDescending(p => p.Fecha)
                 .Take(3)
                 .Select(p => new { p.Id, p.Total })
         })
Assert:  Retorna cliente con exactamente 3 pedidos, los más recientes, solo Id y Total
```

---

### Ciclo 16: Select con múltiples subcollections

**Test:** `Select_MultiplesSubcollections_CargaTodas`

```
Arrange: Crear cliente con pedidos y facturas
Act:     Select(c => new { 
             c.Nombre,
             Pedidos = c.Pedidos.Where(p => p.Estado == "Pendiente"),
             Facturas = c.Facturas.Where(f => f.Pagada == false)
         })
Assert:  Retorna cliente con pedidos pendientes Y facturas no pagadas
```

---

## Fase 5: Query Completa (integración final)

### Ciclo 17: Query completa con todo

**Test:** `QueryCompleta_WhereRoot_SelectConSubcollectionsFiltradas`

```
Arrange: Crear clientes en Madrid y Barcelona
         Cada cliente con pedidos en diferentes estados
         Cada cliente con facturas pagadas y no pagadas
Act:     Where(c => c.Ciudad == "Madrid" && c.Activo)
         .Select(c => new ClienteResumenDto {
             Id = c.Id,
             Nombre = c.Nombre,
             Email = c.Email,
             PedidosPendientes = c.Pedidos
                 .Where(p => p.Estado == "Pendiente" && p.Total > 100)
                 .OrderByDescending(p => p.Fecha)
                 .Select(p => new PedidoResumenDto { Id = p.Id, Total = p.Total }),
             UltimasFacturas = c.Facturas
                 .Where(f => f.Pagada == false)
                 .OrderByDescending(f => f.Fecha)
                 .Take(5)
                 .Select(f => new FacturaResumenDto { Numero = f.Numero, Total = f.Total })
         })
Assert:  
  - Solo clientes de Madrid activos
  - Solo campos Id, Nombre, Email del cliente
  - Pedidos: solo pendientes con total > 100, ordenados, solo Id y Total
  - Facturas: solo no pagadas, últimas 5, solo Numero y Total
```

---

## Fase 6: No soportados

Decidir para cada uno: ¿Client-side o NotSupportedException?

| Operación | Decisión | Test si client-side |
|-----------|----------|---------------------|
| Cálculos | | `Select_Calculo_EjecutaEnCliente` |
| ToUpper/ToLower | | `Select_TransformacionString_EjecutaEnCliente` |
| Substring | | `Select_Substring_EjecutaEnCliente` |
| Interpolación | | `Select_Interpolacion_EjecutaEnCliente` |
| Ternario | | `Select_Condicional_EjecutaEnCliente` |
