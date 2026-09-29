# EXTENSIBILITY_NOTES.md — Cadrage

Note de cadrage pour l'ajout de futures catégories de parts dans le plugin.
**Aucun code n'est implémenté ni engagé ici.** Toute API Civil 3D non encore
vérifiée est marquée `TO VERIFY IN RAG`.

## 1. Où brancher une nouvelle catégorie dans `XdataWriter`

Fichier : `PressureNetworkXdataCommand.cs`
Classe : `internal static class XdataWriter` (partagée par les DLL A et B).

Signature actuelle :

```csharp
public static int WriteAll(Database db, CivilDocument civilDoc)   // ligne 15
```

`WriteAll` ouvre **une seule** transaction (ligne 19), garantit le RegApp puis
appelle les writers dans l'ordre, et commit :

```csharp
EnsureRegApp(tr, db);                          // ligne 23
count += WritePressureNetworks(tr, civilDoc);  // ligne 24
count += WritePipeNetworks(tr, civilDoc);      // ligne 25
tr.Commit();                                   // ligne 26
```

Point d'insertion d'une nouvelle catégorie : **entre la ligne 25 et la ligne 26**,
dans la même transaction.

```csharp
EnsureRegApp(tr, db);
count += WritePressureNetworks(tr, civilDoc);
count += WritePipeNetworks(tr, civilDoc);
count += WriteXxxNetworks(tr, civilDoc);   // <-- nouvelle catégorie
tr.Commit();
```

Contraintes :

- Ne pas ouvrir de transaction imbriquée (utiliser `tr` fourni).
- Ne pas rappeler `EnsureRegApp` (déjà fait une fois par `WriteAll`).
- L'échec global reste géré par le `catch { tr.Abort(); throw; }` de `WriteAll`.

## 2. Contrat d'une méthode writer

Signature recommandée :

```csharp
public static int WriteXxxNetworks(Transaction tr, CivilDocument civilDoc)
```

Étapes attendues :

1. Récupérer les `ObjectId` des conteneurs via une extension `CivilDocument`
   **vérifiée** (du type `civilDoc.GetXxxIds()` → `ObjectIdCollection`).
2. Par conteneur : `tr.GetObject(id, OpenMode.ForRead)` et lire le nom sur la
   propriété Civil **vérifiée** du conteneur — ou, si le nom est porté par la
   part, lire la propriété de la part.
3. Récupérer les `ObjectId` des parts via les `Get...Ids()` du conteneur.
4. Par part : `tr.GetObject(partId, OpenMode.ForWrite)` (le setter `XData` exige
   l'écriture), typé `Autodesk.AutoCAD.DatabaseServices.DBObject`
   (**fully-qualified**, à cause de l'ambiguïté avec
   `Autodesk.Civil.DatabaseServices.DBObject`).
5. Garde-fous par élément : `ObjectId` null/invalide → skip ; échec
   d'ouverture (`Autodesk.AutoCAD.Runtime.Exception`) → skip ; nom null/vide →
   skip.
6. Appeler `SetNetworkNameXData(obj, name)`.
7. Retourner le nombre de parts écrites (le compteur remonte dans `WriteAll`).

Règles transverses :

- Jamais `XData = null` ; le buffer est toujours préfixé `1001, CIVIL3D_PN_NAME`.
- Une seule transaction, celle de `WriteAll`.
- `XdataWriter` est compilé dans les **deux** DLL (`#if MANUAL` / `#if AUTO`) :
  toute nouvelle méthode writer est automatiquement disponible pour A et B.
  Recompiler les deux configurations après ajout.

## 3. Source du nom selon la catégorie

| Catégorie | Source du nom | Statut |
|---|---|---|
| Pressure Network | `PressurePart.NetworkName` (porté par la part) | VERIFIED |
| Pipe Network gravitaire | `Network.Name` (porté par le conteneur, appliqué à toutes les parts) | VERIFIED |
| Toute nouvelle catégorie | à déterminer (conteneur ou part) — **ne pas supposer** | TO VERIFY IN RAG |

## 4. Candidats pour de futures extensions

Tous les candidats ci-dessous sont **TO VERIFY IN RAG** : rien n'est confirmé
pour ces types, ni la propriété de nom, ni la façon d'obtenir les parts, ni
l'héritage vers `DBObject`.

| Objet Civil candidat | Nom naturel | Parts / sous-objets | Statut |
|---|---|---|---|
| `Alignment` | `Name` (?) | entité linéaire + labels (?) | TO VERIFY IN RAG |
| `Corridor` | `Name` (?) | surfaces/regions/baselines (?) | TO VERIFY IN RAG |
| `SampleLineGroup` | `Name` (?) | `SampleLine` (?) | TO VERIFY IN RAG |
| `Profile` / `ProfileView` | `Name` (?) | labels (?) | TO VERIFY IN RAG |
| Pipe Network — types de parts au-delà de `Pipe`/`Structure` (?) | `Network.Name` | `Get...Ids()` (?) | TO VERIFY IN RAG |

Requêtes RAG indicatives (règle de recherche neutre d'`AGENTS.md` : ne pas
mettre la réponse supposée dans la requête) :

```powershell
Get-ChildItem "D:\RAG\Civil3D_API_2026\civapidocs_named" -Filter "*Alignment*Name*" -File
Get-ChildItem "D:\RAG\Civil3D_API_2026\civapidocs_named" -Filter "*Corridor*Name*" -File
Get-ChildItem "D:\RAG\Civil3D_API_2026\civapidocs_named" -Filter "*SampleLineGroup*" -File
Get-ChildItem "D:\RAG\Civil3D_API_2026\civapidocs_named" -Filter "*CivilDocument*Get*Ids*" -File
```

## 5. Checklist avant d'implémenter une extension

1. Vérifier chaque membre Civil dans le RAG (page **membre** exacte, pas une
   page de résumé).
2. Confirmer namespace, assembly, signature complète, type de retour,
   static/instance.
3. Vérifier le type de collection et son mode d'énumération.
4. Vérifier la chaîne d'héritage jusqu'à `DBObject` (indispensable pour XData).
5. Ajouter le writer dans `WriteAll` (transaction unique).
6. Compiler **les deux** configurations (`Release` et `Auto`).
7. Tests runtime dans Civil 3D 2026.

> Document de cadrage uniquement — pas un engagement d'implémentation.
