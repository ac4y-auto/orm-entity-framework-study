# Entity Framework Core - POC

EF Core 10 Proof of Concept projekt InMemory providerrel, .NET 10-en.

## Entity hierarchia

```
Author (1. szint)
  ├── Book (2. szint)  [1:N]
  │     ├── Review (3. szint)  [1:N]
  │     └── Tag  [M:N]
  └── ...
```

## Projekt struktúra

```
├── Models/
│   ├── Author.cs           # Gyökér entity - virtual nav properties (lazy loading)
│   ├── Book.cs             # 2. szint - Author FK + Review/Tag collection
│   ├── Review.cs           # 3. szint (levél) - Book FK
│   └── Tag.cs              # M:N kapcsolat Book-kal
├── Data/
│   └── BookStoreContext.cs  # DbContext, Fluent API konfig, seed data
├── Extensions/
│   └── QueryableExtensions.cs # IncludeAll<T> rekurzív extension method
├── Services/
│   ├── CrudDemos.cs         # Create, Read, Update, Delete műveletek
│   ├── QueryDemos.cs        # LINQ lekérdezések, join, group, subquery, pagination
│   ├── LoadingDemos.cs      # Eager, Explicit, Lazy, Split, Filtered Include, IncludeAll
│   ├── TransactionDemos.cs  # Tranzakciók, concurrency, change tracker
│   └── LazyVsEagerTests.cs  # Lazy vs Eager összehasonlító teszt
└── Program.cs               # Belépési pont
```

## Futtatás

```bash
dotnet run
```

## Technológiák

- .NET 10
- EF Core 10
- InMemory Provider
- Castle.Core (Lazy Loading Proxies)

## Demo tartalom

### CRUD
| Művelet | Példák |
|---------|--------|
| Create | Simple insert, nested child, M:N assign, bulk insert |
| Read | Find by Id, First/Single, projection, count |
| Update | Tracked entity, batch update, disconnected entity |
| Delete | Simple, batch, cascade (Author -> Books -> Reviews) |

### LINQ lekérdezések
- Where, OrderBy, Any, All
- Join (navigation property + explicit LINQ join)
- GroupBy + aggregáció (Count, Average, Sum, Min, Max)
- Subquery (prolific authors, above-average price)
- Pagination (Skip/Take)
- Raw SQL pattern (csak relációs DB-vel működik)

### Loading stratégiák

| Stratégia | Hogyan | Mikor |
|-----------|--------|-------|
| **Eager** | `.Include().ThenInclude()` | Előre tudod mit kell betölteni |
| **Explicit** | `Entry().Collection().Load()` | Feltételesen, később kell betölteni |
| **Lazy** | `virtual` nav property + `.UseLazyLoadingProxies()` | Kényelmes, de N+1 query veszély! |
| **Filtered Include** | `.Include(b => b.Reviews.Where(...))` | Csak a releváns adat kell |
| **IncludeAll** | `.IncludeAll(db)` | Teljes fa betöltése, zero config |

### IncludeAll<T> - rekurzív automatikus betöltés

Saját extension method ami az EF Core `IModel` Metadata API-ból futásidőben felderíti az összes navigation property-t és automatikusan Include-olja őket. **Semmilyen forrásszöveg szintű deklarációt nem igényel** - ami a DbContext modelljében benne van, azt megtalálja.

```csharp
// Kézi Include lánc (törékeny, karbantartandó):
db.Authors.Include(a => a.Books).ThenInclude(b => b.Reviews)
          .Include(a => a.Books).ThenInclude(b => b.Tags)

// IncludeAll - automatikusan betölti a teljes fát:
db.Authors.IncludeAll(db).ToList()
```

Felderített path-ek (runtime):
```
→ Books           (1:N)
→ Books.Reviews   (1:N, 3. szint)
→ Books.Tags      (M:N)
```

Védelmi mechanizmusok:
- **Mélységi limit**: `maxDepth` paraméter (default: 3), `InvalidOperationException` ha túllépi
- **Kör-detekció**: `HashSet<IEntityType>` visited set áganként (Book → Tag → Book megáll)
- **Inverse szűrés**: kiszűri a visszafelé mutatókat (Book.Author kihagyva Author→Books útvonalon)

Metadata API hierarchia amit használ:
```
IModel                        ← db.Model (teljes modell)
  └── IEntityType             ← egy entity metaadata
        ├── GetNavigations()  ← 1:N, N:1 kapcsolatok
        └── GetSkipNavigations() ← M:N kapcsolatok
```

Ha új entity-t adsz a modellhez, az `IncludeAll` **automatikusan megtalálja** - nem kell semmit módosítani.

### Lazy vs Eager teszt

Két külön DbContext-tel demonstrálja a különbséget:

**Lazy Loading (9 query):**
```csharp
// UseLazyLoadingProxies() BEKAPCSOLVA
var authors = db.Authors.ToList();           // Query #1
author.Books.Count;                          // Query #2 - LAZY LOAD!
book.Reviews.Count;                          // Query #3 - LAZY LOAD!
// ... összesen 9 query az N+1 probléma miatt
```

**Eager Loading (1 query):**
```csharp
// UseLazyLoadingProxies() NINCS
var authors = db.Authors
    .Include(a => a.Books)                   // 2. szint
        .ThenInclude(b => b.Reviews)         // 3. szint
    .ToList();                               // EGYETLEN query!
```

**Include nélkül + lazy nélkül = üres collection:**
```csharp
var authors = db.Authors.ToList();
authors[0].Books.Count;  // -> 0  ÜRES!
```

### Tranzakciók és concurrency
- Implicit tranzakció (SaveChanges)
- Explicit tranzakció (BeginTransaction/Commit/Rollback)
- Concurrency conflict handling ([Timestamp]/RowVersion pattern)
- Change Tracker (state tracking, undo, AsNoTracking)

## Megjegyzések

- Az InMemory provider **nem támogatja**: ExecuteUpdate/ExecuteDelete, FromSql, AsSplitQuery, valódi tranzakciókat, concurrency tokeneket
- Ezek a funkciók kommentben/koncepcióként vannak bemutatva, a pattern helyes valódi DB-vel
- `virtual` keyword a navigation property-ken kötelező a lazy loadinghoz
- AutoInclude() szintenként állítható, de rekurzív "töltsd be az egész fát" nem létezik beépítetten EF Core-ban
- Az `IncludeAll<T>()` extension method megoldja ezt az `IModel` Metadata API-n keresztül, zero config
