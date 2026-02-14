# Runbook - EF Core POC

## Futtatás

```bash
dotnet run
```

## Build

```bash
dotnet build
```

## Előfeltételek

- .NET SDK 10+ (`brew install dotnet`)
- Nincs szükség adatbázis szerverre (InMemory provider)

## NuGet csomagok

| Csomag | Miért |
|--------|-------|
| `Microsoft.EntityFrameworkCore.InMemory` | InMemory adatbázis provider |
| `Microsoft.EntityFrameworkCore.Proxies` | Lazy loading proxy generálás (Castle.Core) |

Csomag hozzáadása:
```bash
dotnet add package <csomagnév>
```

## Entity hierarchia

```
Author (gyökér)
  └── Book (1:N)
        ├── Review (1:N)
        └── Tag (M:N, auto join table)
```

## Működési szabályok

### 1. Navigation property-k MINDIG `virtual`

```csharp
public virtual ICollection<Book> Books { get; set; } = new List<Book>();
```

Enélkül a lazy loading proxy nem tud bekapcsolódni.

### 2. DbContext konfiguráció

```csharp
var options = new DbContextOptionsBuilder<BookStoreContext>()
    .UseInMemoryDatabase("BookStoreDb")
    .UseLazyLoadingProxies()                    // lazy loading bekapcsolása
    .ConfigureWarnings(w =>                     // InMemory tranzakció warning elnyomása
        w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
    .Options;
```

### 3. Loading stratégiák választása

| Ha... | Akkor... |
|-------|----------|
| Előre tudod mit kell | `Include().ThenInclude()` (eager) |
| Mindent akarsz | `.IncludeAll(db)` |
| Feltételesen kell | `Entry().Collection().Load()` (explicit) |
| Automatikusan akarod | `.UseLazyLoadingProxies()` (lazy, de N+1!) |

### 4. IncludeAll<T> használata

```csharp
// Teljes fa betöltése:
var authors = db.Authors.IncludeAll(db).ToList();

// Custom mélységi limit:
var authors = db.Authors.IncludeAll(db, maxDepth: 5).ToList();

// Felderített path-ek lekérdezése (debug):
var paths = QueryableExtensions.GetIncludeAllPaths<Author>(db);
```

Szabályok:
- Alapértelmezett maxDepth: 3
- Ha a modell mélyebb → `InvalidOperationException`
- Körkörös referenciák automatikusan kezelve (visited set)
- Inverse back-pointerek (pl. Book.Author) automatikusan kiszűrve

### 5. Seed data

- Skaláris seed data: `BookStoreContext.OnModelCreating()` → `HasData()`
- M:N seed data: `Program.cs` → `SeedManyToMany()` (HasData nem támogatja a join table-t)

### 6. Új entity hozzáadása

1. Hozd létre a Model osztályt a `Models/` mappában
2. Adj `virtual` navigation property-ket a szülő és gyerek entity-kre
3. Add hozzá `DbSet<T>`-ként a `BookStoreContext`-be
4. Konfiguráld a `OnModelCreating`-ben (Fluent API)
5. **Kész** - az `IncludeAll` automatikusan megtalálja

### 7. InMemory provider korlátai

Amit **NEM** támogat:
- `ExecuteUpdate` / `ExecuteDelete` (batch műveletek)
- `FromSql` (raw SQL)
- `AsSplitQuery` / `AsSingleQuery`
- Valódi tranzakciók (BeginTransaction nincs hatása)
- Concurrency tokenek ([Timestamp] nem kényszerített)

Ezek relációs provider-rel (SQL Server, PostgreSQL, SQLite) működnek.
