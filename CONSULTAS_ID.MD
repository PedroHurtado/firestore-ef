# CONSULTAS POR ID

- Cuando buscamos por id deberías buscar con el metodo **GetDocumentAsync** implementado
en FirestoreClientWrapper

Actualmente estás montando la query y buscas por **ExecuteQueryAsync**. Con esto tenemos que asumir que el id está serializado en el documento como atributo y eso no lo vamos a hacer, puesto que estamos duplicando datos

Ejemplos

```csharp
    DbContext.Clientes.FindAsync("cli-001")
    DbContext.Clientes.Where(c=>c.Id=="cli-001")
```

Cualquier consulta de este tipo no va a funcionar.

FindAsync y Where siempre utilizan el mismo camino **ExecuteQueryAsync** poniendo Limit a la query.

## Reglas
- Si se utiliza Where y el Id va a acompañado de cualquier otro atributo error y solo soporta ==

Ejemplos de error

```csharp
    
    DbContext.Clientes.Where(c=>c.Id=="cli-001" && c.Name == "Pedro")

    DbContext.Clientes
        .Where(c=>c.Id=="cli-001")
        .Where(c=>c.Name=="Pedro")
```

Nota: Todo lo demás está solucionado y ahora captura el valor correctamente