# ADR-0001 — Identifiants (`Id` / `Id<TEntity>`)

- **Statut** : Accepté
- **Date** : 2026-05-11
- **Décideurs** : Staff
- **Tags** : domain, identité, sérialisation, persistance, model-binding

---

## Contexte

Chaque agrégat du Domain a besoin d'un identifiant unique.
Sur une plateforme destinée à être réutilisée par plusieurs projets, le choix de cette abstraction conditionne :

1. La **sécurité du typage** au compile-time (interdire qu'un `UserId` soit accidentellement passé là où un `OrderId` est attendu).
2. La **pureté de la couche Domain** (aucune dépendance vers une infrastructure de sérialisation, persistance, ou binding HTTP).
3. La **cohérence** : un `Id` doit se sérialiser, se persister, se binder depuis HTTP, et s'afficher dans un débogueur, **toujours de la même façon**, sans configuration redondante.
4. L'**ergonomie** : ajouter une nouvelle entité ne doit pas nécessiter de toucher à 5 fichiers de configuration.

L'approche par défaut (`Guid` partout, attributs `[JsonConverter]` / `[TypeConverter]` / `[Column]` sur les types Domain) viole les points 1, 2, et partiellement 3.

---

## Objectifs et contraintes

| Objectif / contrainte                 | Pourquoi ?                                                                   |
| ------------------------------------- | ---------------------------------------------------------------------------- |
| Clean Architecture stricte            | Le Domain reste réutilisable, testable sans infra, et compilable seul        |
| Sécurité de typage                    | Erreur la plus fréquente en runtime : mélanger des IDs d'agrégats différents |
| Composition > génération              | Pas de source generator, code lisible et débogable                           |
| Convention > configuration            | Ajouter une entité = zéro ligne de config infra                              |
| Pas d'attributs d'infra sur le Domain | Esthétique, découplage, multi-configurations possibles                       |
| Performance prévisible                | Pas de réflexion en hot path                                                 |
| Testabilité par couche                | Chaque composant infra unit-testable isolément                               |

---

## Référentiel d'architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│ Domain (pur)                                                         │
│   Abstractions/Id.cs                                                 │
│     ├─ struct Id                  (ULID, ISpanParsable, ...)         │
│     └─ struct Id<TEntity>         (phantom type, wrappe Id)          │
│   Aucune référence à Microsoft.* / System.Text.Json / etc.           │
└──────────────────────────────────────────────────────────────────────┘
                                  ▲
                                  │ référence
                                  │
┌──────────────────────────────────────────────────────────────────────┐
│ Infrastructure                                                       │
│                                                                      │
│   ComponentModel/                                                    │
│     ├─ IdTypeConverter                  (legacy interop)             │
│     └─ TypeDescriptorRegistration       (enregistre sans attribut)   │
│                                                                      │
│   Serialization/Json/                                                │
│     ├─ IdJsonConverterFactory           (Id + Id<T> via factory)     │
│     └─ JsonSerializerOptionsExtensions  → AddTypedIdConverters()     │
│                                                                      │
│   Data/                                                              │
│     ├─ Converters/IdConverter.cs        (Id + Id<TEntity> EF)        │
│     ├─ Conventions/IdConvention.cs      (IModelFinalizingConvention) │
│     └─ Extensions/                      → UseDomainIdConventions()   │
│                                                                      │
│   AspNetCore/ModelBinding/                                           │
│     ├─ IdModelBinder<TId>               (générique sur ISpanParsable)│
│     ├─ IdModelBinderProvider                                         │
│     └─ MvcOptionsExtensions             → AddTypedIdModelBinding()   │
│                                                                      │
│   Diagnostics/                                                       │
│     └─ DomainDebuggerDisplays.cs        ([assembly: DebuggerDisplay])│
└──────────────────────────────────────────────────────────────────────┘
                                  ▲
                                  │ référence
                                  │
┌──────────────────────────────────────────────────────────────────────┐
│ Api (composition root)                                               │
│   Program.cs                                                         │
│     • ConfigureHttpJsonOptions(o => o.SerializerOptions              │
│         .AddTypedIdConverters())                                     │
│     • AddControllers(o => o.AddTypedIdModelBinding())   (si MVC)     │
│     • TypeDescriptorRegistration.RegisterIdTypeConverter()           │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Décisions

### D1 — Backing type : ULID

**Options envisagées**

| Option                 | Pour                        | Contre                                                            |
| ---------------------- | --------------------------- | ----------------------------------------------------------------- |
| **ULID** (retenu)      | Time-ordered, compact       | Dépendance NuGet `Ulid`                                           |
| GUID v4                | Natif .NET                  | Aléatoire → index B-tree fragmenté, pas de sortabilité temporelle |
| GUID v7 (RFC 9562)     | Time-ordered, natif .NET 9+ | Adoption récente, moins d'outils, pas de représentation Crockford |
| `long` auto-incrémenté | Petit (8 bytes), trivial    | Non distribuable, devinable, fuit la fraîcheur du système         |

**Choix** : ULID via le package [`Ulid`](https://www.nuget.org/packages/Ulid).

**Conséquences**

- Stockable comme `char(26)` ASCII (lexicographiquement ordonné = temporellement ordonné).
- Compatible GUID via `Id.ToGuid()` / `Id.FromGuid()` pour interop legacy, **avec perte de la temporalité dans le sens GUID → ULID**.
- Trie temporellement → index B-tree efficace, 128 bits, format texte compact (26 chars), lisible et copiable (Crockford Base32, sans caractères confus).

---

### D2 — Wrapper `Id<TEntity>` sur `Id` (pas modification, pas types nominaux)

**Options envisagées**

| Option                                          | Pour                   | Contre                                                                   |
| ----------------------------------------------- | ---------------------- | ------------------------------------------------------------------------ |
| **Phantom type `Id<TEntity>` wrappant `Id`**    | Une seule classe       | Friction aux frontières DTO (`Id<UserEntity>` ≠ `Id<UserDto>`)           |
| Types nominaux par entité (`UserId`, `OrderId`) | Plus explicite         | Boilerplate massif sans source generator                                 |
| Source generator                                | Zéro boilerplate       | Coût de build, dépendance externe (rejetée), code généré moins débogable |
| Modifier `Id` pour le rendre générique          | Élimine la duplication | Breaking pour tout code existant utilisant `Id`, perte du type-erased    |

**Choix** : `Id<TEntity>` est une struct générique wrappant un `Id` privé. `TEntity` est un phantom type (sans représentation runtime).

**Conséquences**

- `Id` non-typé reste disponible pour les contextes type-erased (audit logs, dictionnaires...).
- Les conversions sont **explicites** entre `Id<TEntity>` et `Id` (`ToId()` / `FromId()`).
- Pas de conversion possible entre `Id<A>` et `Id<B>` directement : il faut passer par `Id` non-typé.
- Le wrapper coûte 0 octet runtime supplémentaire (struct générique spécialisée, layout identique).

---

### D3 — Pas d'attributs d'infrastructure sur les types Domain

**Décision** : les types `Id` et `Id<TEntity>` ne portent **aucun attribut** lié à une infrastructure :

- Pas de `[JsonConverter(...)]`
- Pas de `[TypeConverter(...)]`
- Pas de `[Column(...)]` / annotations EF Core

**Pourquoi**

1. **Couplage** : un attribut force l'assembly Domain à référencer le namespace de l'attribut (et donc transitivement la lib infrastructure).
2. **Mono-configuration** : un attribut fige une config unique ; impossible d'avoir des sérialisations différentes selon le contexte (API publique vs. file de messages internes).
3. **Lisibilité** : un dev qui lit le Domain doit pouvoir le comprendre sans connaître la stack infra.

**Conséquences**

- La configuration vit dans la couche infrastructure, **enregistrée par extension method** sur le type d'options idiomatique du framework cible (`JsonSerializerOptions`, `ModelConfigurationBuilder`, `MvcOptions`).
- `[DebuggerDisplay]` est l'exception tolérée — purement diagnostic, BCL only — mais elle est **également externalisée** via `[assembly: DebuggerDisplay(..., Target = typeof(Id))]` dans Infrastructure pour stricte cohérence (voir D8).

---

### D4 — Sérialisation JSON : `JsonConverterFactory`

**Options envisagées**

| Option                                           | Pour                          | Contre                                                         |
| ------------------------------------------------ | ----------------------------- | -------------------------------------------------------------- |
| **`JsonConverterFactory` unique** (retenu)       | Une seule méthode d'extension | Légère réflexion via `MakeGenericType` une fois par type fermé |
| Converter par type fermé enregistré manuellement | Pas de réflexion              | Chaque nouveau type d'entité impose une ligne de config        |
| `[JsonConverter(...)]` sur les types             | Auto-détecté par STJ          | Viole D3                                                       |

**Choix** : `IdJsonConverterFactory` qui produit dynamiquement un `JsonConverter<Id>` ou `JsonConverter<Id<T>>` selon le type rencontré. Sérialisation = string ULID (26 chars).

**Conséquences**

- Activation : `options.AddTypedIdConverters()`.
- Coût `Activator.CreateInstance` payé **une fois par type fermé** (STJ cache le converter retourné).
- Compatible Minimal APIs ET Controllers (même pipeline JSON).

---

### D5 — Mapping EF Core : `IModelFinalizingConvention`

**Options envisagées**

| Option                                           | Pour                     | Contre                                                |
| ------------------------------------------------ | ------------------------ | ----------------------------------------------------- |
| **Convention EF (`IModelFinalizingConvention`)** | Idiomatique EF           | API conventions un peu plus avancée                   |
| Scan d'assembly Domain à `ConfigureConventions`  | Code simple à comprendre | `Assembly.GetTypes()` coûteux, couplage à un assembly |
| `[Column]` / Fluent API par entité               | Explicite                | Massivement répétitif sur N entités                   |

**Choix** : `IdConvention` plug dans la `ConventionSet` d'EF Core via `configurationBuilder.Conventions.Add(_ => new IdConvention())`. La convention itère sur le model EF en cours de finalisation et applique le mapping aux propriétés `Id`, `Id?`, `Id<T>`, `Id<T>?`.

**Storage choisi** : `char(26)`, non-unicode, collation `C` (binary).

**Conséquences**

- Performance : ~1 ms à l'init du DbContext (model EF est ensuite mis en cache).
- Ajout d'une nouvelle entité : **zéro ligne de configuration EF**.
- Étendable : `EmailAddressConvention`, `MoneyConvention` se branchent de la même façon.
- L'ordre lexicographique des ULID Crockford correspond à l'ordre temporel → `ORDER BY id` donne l'ordre chronologique sans surcoût.

---

### D6 — Model binding ASP.NET MVC : `IModelBinderProvider`

**Options envisagées**

| Option                                                 | Pour                   | Contre                                                       |
| ------------------------------------------------------ | ---------------------- | ------------------------------------------------------------ |
| **`IModelBinderProvider` générique**                   | Couvre `Id` et `Id<T>` | Code custom (~50 lignes au total)                            |
| `TypeConverter` (`SimpleTypeModelBinder`)              | Mécanisme natif MVC    | Ne fonctionne pas pour les types génériques fermés (`Id<T>`) |
| Custom `[ModelBinder(typeof(...))]` sur les paramètres | Granulaire             | Viole D3, force du boilerplate au point d'usage              |

**Choix** : `IdModelBinder<TId>` générique avec contrainte `where TId : struct, ISpanParsable<TId>`. Le provider détecte `Id` et `Id<T>` et instancie le binder approprié.

**Conséquences**

- Activation : `mvcOptions.AddTypedIdModelBinding()`.
- **Pour les Minimal APIs : RIEN à faire** — le framework détecte `IParsable<TSelf>` automatiquement.
- Le binder est inséré en tête (`Insert(0, ...)`) pour priorité sur le `SimpleTypeModelBinder` (qui passerait par le TypeConverter).

---

### D7 — Legacy interop : `TypeConverter` via `TypeDescriptor.AddAttributes`

**Pour quoi faire**
Quelques consommateurs résolvent encore les conversions string ↔ `Id` via `System.ComponentModel.TypeDescriptor.GetConverter(...)` plutôt que par le pipeline MVC moderne : `System.Configuration` (lecture typée d'`<appSettings>`), certains bindings XAML / Properties grid des designers, et des libs tierces qui inspectent les `TypeConverter` par réflexion. Pour un backend pur ASP.NET Core (Minimal API + EF Core), ce point n'est en pratique **pas nécessaire** — il est conservé comme filet de sécurité.

**Choix** : `IdTypeConverter` reste défini en Infrastructure, mais **n'est pas attaché via attribut**. Son enregistrement se fait au démarrage :

```csharp
TypeDescriptorRegistration.RegisterIdTypeConverter();
```

Sous le capot : `TypeDescriptor.AddAttributes(typeof(Id), new TypeConverterAttribute(typeof(IdTypeConverter)))`.

**Limites**

- Cette approche **ne couvre que `Id` non-générique**. Pour `Id<T>`, il faudrait enregistrer chaque type fermé individuellement — l'effort est jugé non rentable. Les frameworks modernes utilisent le model binder (D6).
- C'est une **mutation globale du registre `TypeDescriptor`** — à appeler une seule fois au démarrage de l'app.

---

### D8 — Debugger display : assembly-level dans Infrastructure

**Choix** : utiliser la propriété `Target` de `DebuggerDisplayAttribute` pour appliquer le display depuis un fichier de l'assembly Infrastructure, sans toucher au Domain :

```csharp
// Infrastructure/Diagnostics/DomainDebuggerDisplays.cs
[assembly: DebuggerDisplay("{ToString(),nq}", Target = typeof(Id))]
[assembly: DebuggerDisplay("{ToString(),nq}", Target = typeof(Id<>))]
```

`typeof(Id<>)` (open generic) couvre **tous les types fermés** (`Id<User>`, `Id<Order>`, ...) en une seule ligne.

**Conséquences**

- Domain 100% pur (zéro using `System.Diagnostics`).
- Le pretty display dépend de l'assembly Infrastructure chargée — si une suite de tests charge uniquement Domain, le débogueur retombera sur la représentation par défaut (`Id { _value = ... }`). Acceptable.

---

## Architecture résultante

| Préoccupation             | Fichier                                                    | Activation côté Program.cs                             |
| ------------------------- | ---------------------------------------------------------- | ------------------------------------------------------ |
| Représentation du type    | `src/Domain/Abstractions/Id.cs`                            | —                                                      |
| JSON (body, response)     | `src/Infrastructure/Serialization/Json/`                   | `o.SerializerOptions.AddTypedIdConverters()`           |
| EF Core (persistance)     | `src/Infrastructure/Data/Conventions/IdConvention.cs`      | `configurationBuilder.UseDomainIdConventions()`        |
| MVC binding (Controllers) | `src/Infrastructure/AspNetCore/ModelBinding/`              | `o.AddTypedIdModelBinding()`                           |
| Legacy interop            | `src/Infrastructure/ComponentModel/`                       | `TypeDescriptorRegistration.RegisterIdTypeConverter()` |
| Debug display             | `src/Infrastructure/Diagnostics/DomainDebuggerDisplays.cs` | Automatique                                            |
| Minimal APIs              | (BCL `IParsable<TSelf>`)                                   | Automatique                                            |

---

## Composition root recommandée

```csharp
using Infrastructure.AspNetCore.ModelBinding;
using Infrastructure.ComponentModel;
using Infrastructure.Serialization.Json;

var builder = WebApplication.CreateBuilder(args);

// Sérialisation JSON (Minimal APIs et Controllers)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.AddTypedIdConverters());

// Controllers MVC (si utilisés en plus des Minimal APIs)
builder.Services
    .AddControllers(o => o.AddTypedIdModelBinding())
    .AddJsonOptions(o => o.JsonSerializerOptions.AddTypedIdConverters());

// Interop legacy (frameworks tiers consommant System.ComponentModel)
TypeDescriptorRegistration.RegisterIdTypeConverter();

// EF Core : mapping configuré dans ApplicationDatabaseContext.ConfigureConventions
//   via configurationBuilder.UseDomainIdConventions()
```

---

## Recette : ajouter un nouvel ID typé

```csharp
// Dans Domain/Users/User.cs
public sealed record User(Id<User> Id, string Name);

// Dans Domain/Orders/Order.cs
public sealed record Order(Id<Order> Id, Id<User> CustomerId, decimal Total);
```

**C'est tout.** Aucune config infra à ajouter :

- JSON → factory détecte `Id<User>` et `Id<Order>` automatiquement
- EF Core → la convention détecte les propriétés `Id<*>` lors du model-building et applique le mapping `char(26)`
- MVC → le binder provider détecte les types fermés et instancie le binder

---

## Recette : tester un converter / convention

```csharp
// Infrastructure.UnitTests/Data/IdConventionTests.cs
public class IdConventionTests
{
    private sealed class Sample
    {
        public Id Id { get; init; }
        public Id<Sample> TypedId { get; init; }
        public Id? Optional { get; init; }
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        public DbSet<Sample> Samples => Set<Sample>();
        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
            => builder.UseDomainIdConventions();
    }

    [Fact]
    public void Maps_Id_and_typed_Id_to_char26()
    {
        using var ctx = new TestContext(
            new DbContextOptionsBuilder<TestContext>().UseInMemoryDatabase("t").Options);

        var entity = ctx.Model.FindEntityType(typeof(Sample))!;

        entity.FindProperty(nameof(Sample.Id))!.GetColumnType().ShouldBe("char(26)");
        entity.FindProperty(nameof(Sample.TypedId))!.GetColumnType().ShouldBe("char(26)");
        entity.FindProperty(nameof(Sample.Optional))!.GetColumnType().ShouldBe("char(26)");
    }
}
```

---

## Conséquences

### Positives

- **Domain strictement pur** : aucun `using` explicite dans `Id.cs` ; les types référencés sont tous dans `System` (le package `Ulid` expose son type dans `System`, accessible via les implicit usings du SDK).
- **Sécurité de typage** : impossible de mélanger `Id<User>` et `Id<Order>` au compile-time.
- **Zéro config par entité** : ajouter une entité ne touche aucun fichier infra.
- **Convention-driven** : extensible à d'autres value objects (Money, EmailAddress) via le même pattern.
- **Testable par couche** : chaque composant infra unit-testable contre un model EF/STJ minimal.
- **Multi-config possible** : on peut avoir un `JsonSerializerOptions` pour l'API publique et un autre pour les messages internes, chacun avec son propre comportement.

### Négatives

- **Friction aux frontières DTO** : `Id<UserEntity>` et `Id<UserDto>` sont distincts → conversion explicite requise dans la couche de mapping.
- **Plus de code** : ~325 lignes en infrastructure (factory JSON, convention EF, model binder, type converter, registrations). L'alternative « attributs sur le Domain » garderait les classes de converters (~150 lignes) et économiserait surtout les méthodes d'extension. Compensé par la réutilisabilité et la testabilité.
- **Légère réflexion** au model-building EF Core (`MakeGenericType` + `Activator.CreateInstance` une fois par type fermé). Négligeable, amorti par le cache de model EF.
- **Pretty display debug perdu** quand Domain est chargé sans Infrastructure (cas rare : tests unitaires purs).

---

## Limites connues et travaux futurs

2. **OpenAPI / Swagger** : les schémas génèrent actuellement le type complet `Id<TEntity>` comme objet imbriqué. Un `ISchemaFilter` est à ajouter pour les exposer comme `type: string, format: ulid`.
3. **Dapper / micro-ORMs** : si un projet de la plateforme adopte Dapper, ajouter `SqlMapper.AddTypeHandler<Id>(...)` et un type handler générique pour `Id<T>` (réflexion similaire au JSON factory).
4. **Tests d'architecture** : un `NetArchTest` à ajouter dans `tests/Architecture.Tests` pour interdire toute référence de Domain vers `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`, `System.Text.Json.*`. Cela gèle les frontières et empêche une régression accidentelle.

---

## Références

### Fichiers du codebase

- `src/Domain/Abstractions/Id.cs` — les structs `Id` et `Id<TEntity>`
- `src/Infrastructure/Serialization/Json/IdJsonConverterFactory.cs`
- `src/Infrastructure/Data/Conventions/IdConvention.cs`
- `src/Infrastructure/Data/Converters/IdConverter.cs`
- `src/Infrastructure/AspNetCore/ModelBinding/IdModelBinder.cs`
- `src/Infrastructure/AspNetCore/ModelBinding/IdModelBinderProvider.cs`
- `src/Infrastructure/ComponentModel/IdTypeConverter.cs`
- `src/Infrastructure/Diagnostics/DomainDebuggerDisplays.cs`

### Externe

- [ULID spec](https://github.com/ulid/spec)
- [.NET `IParsable<TSelf>` documentation](https://learn.microsoft.com/dotnet/api/system.iparsable-1)
- [EF Core conventions](https://learn.microsoft.com/ef/core/modeling/bulk-configuration#conventions)
- [ASP.NET Core model binding](https://learn.microsoft.com/aspnet/core/mvc/models/model-binding)
- [MADR format](https://adr.github.io/madr/)
