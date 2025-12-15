# Tests TDD: Where - EF Core Firestore Provider

**Fecha:** 2025-12-15

---

## Fase 1: Comparaciones

### Ciclo 1: Igualdad

**Test:** `Where_Igualdad_RetornaCoincidencias`

```
Arrange: Crear productos con diferentes categorías
Act:     Filtrar por categoría específica con ==
Assert:  Solo retorna productos de esa categoría
```

---

### Ciclo 2: Desigualdad

**Test:** `Where_Desigualdad_ExcluyeCoincidencias`

```
Arrange: Crear productos con diferentes categorías
Act:     Filtrar excluyendo una categoría con !=
Assert:  Retorna todos excepto la categoría excluida
```

---

### Ciclo 3: Mayor que

**Test:** `Where_MayorQue_RetornaValoresMayores`

```
Arrange: Crear productos con precios 50, 100, 150
Act:     Filtrar precio > 100
Assert:  Solo retorna producto con precio 150
```

---

### Ciclo 4: Mayor o igual

**Test:** `Where_MayorOIgual_IncluyeLimite`

```
Arrange: Crear productos con precios 50, 100, 150
Act:     Filtrar precio >= 100
Assert:  Retorna productos con precio 100 y 150
```

---

### Ciclo 5: Menor que

**Test:** `Where_MenorQue_RetornaValoresMenores`

```
Arrange: Crear productos con precios 50, 100, 150
Act:     Filtrar precio < 100
Assert:  Solo retorna producto con precio 50
```

---

### Ciclo 6: Menor o igual

**Test:** `Where_MenorOIgual_IncluyeLimite`

```
Arrange: Crear productos con precios 50, 100, 150
Act:     Filtrar precio <= 100
Assert:  Retorna productos con precio 50 y 100
```

---

## Fase 2: Lógicos

### Ciclo 7: AND

**Test:** `Where_And_AplicaAmbasCondiciones`

```
Arrange: Crear productos variando categoría y precio
Act:     Filtrar categoría == "X" && precio > 100
Assert:  Solo retorna los que cumplen ambas condiciones
```

---

### Ciclo 8: OR

**Test:** `Where_Or_RetornaCualquierCondicion`

```
Arrange: Crear productos variando categoría y precio
Act:     Filtrar categoría == "X" || precio > 100
Assert:  Retorna los que cumplen al menos una condición
```

---

### Ciclo 9: AND + OR combinado

**Test:** `Where_AndOrCombinado_RespetaPrecedencia`

```
Arrange: Crear productos variando categoría, precio y activo
Act:     Filtrar categoría == "A" && (precio > 100 || activo)
Assert:  Respeta precedencia de paréntesis
```

---

## Fase 3: IN

### Ciclo 10: IN

**Test:** `Where_In_RetornaValoresEnLista`

```
Arrange: Crear productos con categorías A, B, C
Act:     Filtrar donde categoría está en [A, B]
Assert:  Retorna productos de categorías A y B
```

---

### Ciclo 11: NOT IN

**Test:** `Where_NotIn_ExcluyeValoresEnLista`

```
Arrange: Crear productos con categorías A, B, C
Act:     Filtrar donde categoría NO está en [A, B]
Assert:  Solo retorna productos de categoría C
```

---

## Fase 4: Null y Boolean

### Ciclo 12: Es null

**Test:** `Where_EsNull_RetornaCamposNull`

```
Arrange: Crear productos con y sin descripción
Act:     Filtrar descripción == null
Assert:  Solo retorna productos sin descripción
```

---

### Ciclo 13: No es null

**Test:** `Where_NoEsNull_ExcluyeCamposNull`

```
Arrange: Crear productos con y sin descripción
Act:     Filtrar descripción != null
Assert:  Solo retorna productos con descripción
```

---

### Ciclo 14: Boolean true

**Test:** `Where_BoolTrue_RetornaActivos`

```
Arrange: Crear productos activos e inactivos
Act:     Filtrar activo == true
Assert:  Solo retorna productos activos
```

---

### Ciclo 15: Boolean false

**Test:** `Where_BoolFalse_RetornaInactivos`

```
Arrange: Crear productos activos e inactivos
Act:     Filtrar activo == false
Assert:  Solo retorna productos inactivos
```

---

## Fase 5: Arrays

### Ciclo 16: Array contains

**Test:** `Where_ArrayContains_EncuentraElemento`

```
Arrange: Crear productos con arrays de tags diferentes
Act:     Filtrar donde tags contiene "portátil"
Assert:  Solo retorna productos con ese tag
```

---

### Ciclo 17: Array contains any

**Test:** `Where_ArrayContainsAny_EncuentraCualquierElemento`

```
Arrange: Crear productos con arrays de tags diferentes
Act:     Filtrar donde tags contiene alguno de [X, Y]
Assert:  Retorna productos que tengan X o Y en tags
```

---

## Fase 6: Ordenamiento

### Ciclo 18: OrderBy

**Test:** `OrderBy_OrdenaAscendente`

```
Arrange: Crear productos con nombres desordenados
Act:     Ordenar por nombre ascendente
Assert:  Resultados en orden alfabético A-Z
```

---

### Ciclo 19: OrderByDescending

**Test:** `OrderByDescending_OrdenaDescendente`

```
Arrange: Crear productos con precios variados
Act:     Ordenar por precio descendente
Assert:  Resultados de mayor a menor precio
```

---

### Ciclo 20: ThenBy

**Test:** `ThenBy_OrdenaSegundoCriterio`

```
Arrange: Crear productos con misma categoría, diferentes nombres
Act:     Ordenar por categoría, luego por nombre
Assert:  Orden correcto por ambos criterios
```

---

### Ciclo 21: ThenByDescending

**Test:** `ThenByDescending_OrdenaSegundoCriterioDescendente`

```
Arrange: Crear productos con misma categoría, diferentes precios
Act:     Ordenar por categoría asc, luego por precio desc
Assert:  Orden correcto por ambos criterios
```

---

## Fase 7: Límites

### Ciclo 22: Take

**Test:** `Take_LimitaResultados`

```
Arrange: Crear 10 productos
Act:     Take(3)
Assert:  Solo retorna 3 productos
```

---

### Ciclo 23: First / FirstOrDefault

**Test:** `First_RetornaPrimerElemento`

```
Arrange: Crear varios productos
Act:     First() con filtro
Assert:  Retorna un solo producto
```

**Test:** `FirstOrDefault_RetornaNullSiNoHay`

```
Arrange: Crear productos que no cumplen filtro
Act:     FirstOrDefault() con filtro sin coincidencias
Assert:  Retorna null
```

---

### Ciclo 24: Single / SingleOrDefault

**Test:** `Single_RetornaUnicoElemento`

```
Arrange: Crear un producto que cumple filtro
Act:     Single() con filtro
Assert:  Retorna ese producto
```

**Test:** `Single_LanzaExcepcionSiHayMasDeUno`

```
Arrange: Crear varios productos que cumplen filtro
Act:     Single() con filtro
Assert:  Lanza InvalidOperationException
```

---

### Ciclo 25: Skip

**Test:** `Skip_SaltaElementos`

```
Arrange: Crear 10 productos ordenados
Act:     Skip(3).Take(3)
Assert:  Retorna elementos 4, 5, 6
```

**Nota:** Documentar ineficiencia en Firestore (Offset cobra por documentos saltados)

---

## Fase 8: Agregaciones

### Ciclo 26: Count

**Test:** `Count_CuentaElementos`

```
Arrange: Crear 5 productos de categoría A, 3 de categoría B
Act:     Count() con filtro categoría == A
Assert:  Retorna 5
```

---

### Ciclo 27: Any

**Test:** `Any_RetornaTrueSiExiste`

```
Arrange: Crear productos con categoría específica
Act:     Any() con filtro
Assert:  Retorna true
```

**Test:** `Any_RetornaFalseSiNoExiste`

```
Arrange: Crear productos sin categoría buscada
Act:     Any() con filtro sin coincidencias
Assert:  Retorna false
```

---

### Ciclo 28: Sum

**Test:** `Sum_SumaValores`

```
Arrange: Crear productos con precios 10, 20, 30
Act:     Sum(x => x.Precio)
Assert:  Retorna 60
```

---

### Ciclo 29: Average

**Test:** `Average_CalculaPromedio`

```
Arrange: Crear productos con precios 10, 20, 30
Act:     Average(x => x.Precio)
Assert:  Retorna 20
```

---

### Ciclo 30: Min

**Test:** `Min_RetornaMinimo`

```
Arrange: Crear productos con precios 10, 20, 30
Act:     Min(x => x.Precio)
Assert:  Retorna 10
```

**Nota:** Client-side - Firestore no soporta nativamente

---

### Ciclo 31: Max

**Test:** `Max_RetornaMaximo`

```
Arrange: Crear productos con precios 10, 20, 30
Act:     Max(x => x.Precio)
Assert:  Retorna 30
```

**Nota:** Client-side - Firestore no soporta nativamente

---

## Fase 9: Límites adicionales

### Ciclo 32: TakeLast

**Test:** `TakeLast_RetornaUltimosElementos`

```
Arrange: Crear 10 productos ordenados
Act:     TakeLast(3)
Assert:  Retorna últimos 3 elementos
```

---

## Fase 10: Strings

### Ciclo 33: StartsWith

**Test:** `Where_StartsWith_FiltraPorPrefijo`

```
Arrange: Crear productos "Laptop HP", "Laptop Dell", "Mouse"
Act:     Filtrar nombre.StartsWith("Laptop")
Assert:  Retorna los dos laptops
```

**Nota:** Implementar con workaround >= y < en Firestore

---

## Fase 11: No soportados

Decidir para cada uno: ¿Client-side o NotSupportedException?

| Operación | Decisión | Test si client-side |
|-----------|----------|---------------------|
| EndsWith | | `Where_EndsWith_FiltraPorSufijo` |
| Contains (string) | | `Where_Contains_FiltraPorSubstring` |
| Like | | `Where_Like_FiltraConPatron` |
| IgnoreCase | | `Where_IgnoreCase_ComparaInsensible` |
