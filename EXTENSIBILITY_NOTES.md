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

## 6. Renommage des parts — reconnaissance RAG (RÉSOLU — voir §8)

Recherche dans `D:\RAG\Civil3D_API_2026\civapidocs_named\` (v13.8.0.292) :
déterminer si `.Name` des parts est lecture seule ou lecture/écriture, et
confirmer la chaîne d'héritage jusqu'à
`Autodesk.AutoCAD.DatabaseServices.DBObject` (accès à `.Handle`).
Ce RAG **ne contient aucune page AutoCAD/ObjectARX** (seulement Civil 3D).

### 6.1 Tableau des membres

| Membre | Signature exacte | Lecture seule ? | Statut | Fichier RAG source |
|---|---|---|---|---|
| `Autodesk.Civil.DatabaseServices.Entity.Name` | `public virtual string Name { get; set; }` | non (get/set) | VERIFIED | `Entity.Name_Property.htm` |
| `Autodesk.Civil.DatabaseServices.DBObject.Name` | `public virtual string Name { get; set; }` | non (get/set) | VERIFIED | `DBObject.Name_Property.htm` |
| `Autodesk.Civil.DatabaseServices.Part.Name` | `public override string Name { get; }` | **oui** | VERIFIED | `Part.Name_Property.htm` |
| `Autodesk.Civil.DatabaseServices.PressurePart.Name` | `public override string Name { get; }` | **oui** | VERIFIED | `PressurePart.Name_Property.htm` |
| `PressurePipe.Name` | héritée de `PressurePart` (aucune redéfinition propre) | **oui** | VERIFIED | `PressurePipe_Properties.htm` |
| `PressureFitting.Name` | héritée de `PressurePart` | **oui** | VERIFIED | `PressureFitting_Properties.htm` |
| `Pipe.Name` | héritée de `Part` | **oui** | VERIFIED | `Pipe_Properties.htm` |
| `Structure.Name` | héritée de `Part` | **oui** | VERIFIED | `Structure_Properties.htm` |
| `Autodesk.AutoCAD.DatabaseServices.DBObject.Handle` | non documenté dans ce RAG | ? | **UNVERIFIED** | aucune page trouvée |

Extraits (signatures brutes) :

- `Part.Name_Property.htm` : « Part Name Property … Gets part's name. …
  `public override string Name { get ; }` ».
- `PressurePart.Name_Property.htm` : « Pressure Part Name Property … Gets
  part's name. … `public override string Name { get ; }` ».
- `Entity.Name_Property.htm` : « Gets or sets the Entity object's name. …
  `public virtual string Name { get ; set ; }` ».
- `PressurePipe_Properties.htm` : « Name Gets part's name.
  (Inherited from PressurePart ) ».
- `PressureFitting_Properties.htm` : « Name Gets part's name.
  (Inherited from PressurePart ) ».
- `Pipe_Properties.htm` : « Name Gets part's name. (Inherited from Part ) ».
- `Structure_Properties.htm` : « Name Gets part's name. (Inherited from Part ) ».

**Conclusion** : `.Name` des 4 types demandés est **en LECTURE SEULE**
(override sans setter au niveau `Part` / `PressurePart`). Un renommage via
`.Name` n'est donc pas possible par l'API managée.

### 6.2 Chaînes d'héritage vers `DBObject`

Chaîne lue dans les pages de classe (`… Drawable DBObject Entity Curve Entity
Autodesk.Civil.DatabaseServices Entity …`) :

- `PressurePipe` → `PressurePart` → `GeoEntity` →
  `Autodesk.Civil.DatabaseServices.Entity` →
  `Autodesk.AutoCAD.DatabaseServices.Curve` →
  `Autodesk.AutoCAD.DatabaseServices.Entity` →
  `Autodesk.AutoCAD.DatabaseServices.DBObject`
- `PressureFitting` → idem via `PressurePart`.
- `PressureAppurtenance` → idem via `PressurePart`.
- `Pipe` → `Part` → `GeoEntity` → `Autodesk.Civil.DatabaseServices.Entity` →
  `Autodesk.AutoCAD.DatabaseServices.Curve` →
  `Autodesk.AutoCAD.DatabaseServices.Entity` →
  `Autodesk.AutoCAD.DatabaseServices.DBObject`
- `Structure` → idem via `Part`.

Sources : `PressurePipe_Class.htm`, `PressureFitting_Class.htm`,
`PressureAppurtenance_Class.htm`, `PressurePart_Class.htm`, `Part_Class.htm`,
`Pipe_Class.htm`, `Structure_Class.htm`, `Entity_Class.htm`,
`GeoEntity_Class.htm`.

`Autodesk.AutoCAD.DatabaseServices.DBObject` est donc bien un ancêtre des
4 types.

### 6.3 `.Handle`

`DBObject.Handle` n'est **pas documenté** dans ce RAG Civil 3D (le RAG ne
documente pas les membres AutoCAD hérités). Recherches effectuées :

- nom de fichier `*Handle*` : aucune page `DBObject.Handle` / `Handle_Property` ;
- contenu `Autodesk.AutoCAD.DatabaseServices.DBObject` : **0 occurrence** ;
- contenu « Gets the handle » / « Gets or sets the handle » : **0 occurrence**.

Statut : **UNVERIFIED (from RAG)**. Piste : consulter la documentation AutoCAD
ObjectARX Managed Reference pour `Autodesk.AutoCAD.DatabaseServices.DBObject`
(assembly `acdbmgd`), ou vérifier par réflexion sur `acdbmgd.dll`. Le type
attendu serait `Autodesk.AutoCAD.DatabaseServices.Handle` en **lecture seule**,
et `Handle.ToString()` renverrait l'hexadécimal — **non prouvé par ce RAG**.

### 6.4 Longueur maximale / caractères interdits

Aucune contrainte documentée pour `Part.Name` / `PressurePart.Name` : pas de
section « Exceptions », aucune remarque sur la longueur ou les caractères. Le
RAG documente ce type de contrainte pour d'autres types (ex.
`AssemblyGroup.Name_Property.htm` : `ArgumentNullException` si null,
`ArgumentException` si vide, dupliqué ou caractères invalides), mais **rien
d'équivalent** pour les parts. Statut : **UNVERIFIED / non documenté**.

### 6.5 `PressurePart.NetworkName` / `Network.Name` vs renommage d'une part

- `PressurePart.NetworkName` (`PressurePart.NetworkName_Property.htm`) :
  « Gets the name of the network to which this part belongs. » — propriété
  **lecture seule**, portée par le conteneur du réseau, distincte de `Name`.
- `Network.Name` (`Network_Class.htm` + `Entity.Name_Property.htm`) : porté
  par le conteneur `Network`.
- Aucune documentation n'établit de lien entre le `Name` d'une part et le nom
  du réseau. De plus, `Part.Name` / `PressurePart.Name` étant **lecture seule**,
  aucune modification du `.Name` d'une part n'est possible via l'API.
- Conclusion : **pas d'effet** attendu sur `NetworkName` / `Network.Name`
  (raisonnement + lecture seule ; non documenté explicitement → à confirmer au
  runtime si nécessaire).

## 7. Renommage manuel vs API — approfondissement (RÉSOLU — voir §8)

Contexte : l'UI Civil 3D 2026 permet de renommer manuellement une part (champ
« Name » éditable). Recherche de la voie d'écriture : (a) API managée non
documentée, ou (b) COM/interne.

### 7.1 Tâche 1 — méthodes RAG (`Part/PressurePart/Pipe/..._Methods.htm`)

Pages de méthodes lues : `Part_Methods.htm`, `PressurePart_Methods.htm`,
`Pipe_Methods.htm`, `PressurePipe_Methods.htm`, `PressureFitting_Methods.htm`,
`Structure_Methods.htm`.

- Aucune occurrence de `SetName` ni `Rename` dans ces pages (la seule
  correspondance « Name » est l'en-tête de tableau « Name Description »).
- Recherche globale `SetName|Rename` dans tout le RAG : aucune méthode de
  renommage de part. Fichiers voisins trouvés (non applicables) :
  `Folder.RenameFolder_Method.htm` (dossiers Prospector),
  `SettingsCmdRenamePipeNetworkParts_Class.htm` (classe de **settings** de la
  commande UI « RenamePipeNetworkParts », `Autodesk.Civil.Settings`,
  `: SettingsPipeNetwork` — pas d'API d'exécution),
  `Alignment.DesignCheckSetName_Property.htm`,
  `Profile.DesignCheckSetName_Property.htm`,
  `Catchment.ReferencePipeNetworkStructureName_Property.htm`,
  `Network.StructureNameTemplate_Property.htm`.

Statut : **`SetName` / `Rename` ABSENT DU RAG** pour Part, PressurePart, Pipe,
PressurePipe, PressureFitting, Structure.

### 7.2 Tâche 2 — API COM

- Aucun fichier `AeccXUi*`, `AeccPipe*`, `AeccPressurePart*` dans le RAG.
- Recherche par contenu `AeccXUi|AeccPressurePart|IAecc|AeccPipe|COM` : **0
  occurrence**.

Statut : **UNVERIFIED** — ce RAG est strictement .NET (Civil 3D) et ne
documente pas l'API COM. Impossible de confirmer `Let`/`Put` ici. Piste :
documentation COM Civil 3D séparée ou inspection de la base de registre /
type library (`AeccXUi*.tlb` / interop).

### 7.3 Tâche 3 — réflexion MetadataLoadContext (`AeccDbMgd.dll`, `AeccPressurePipesMgd.dll`)

Membres contenant « Name » (visibilité get/set exacte) :

| Type | Membre | get | set | Déclaré dans | Assembly |
|---|---|---|---|---|---|
| `Part` | `Name` | `public` | **aucun** | `Part` | AeccDbMgd |
| `PressurePart` | `Name` | `public` | **aucun** | `PressurePart` | AeccPressurePipesMgd |
| `Pipe` | `Name` | `public` | **aucun** | `Part` (hérité) | AeccDbMgd |
| `PressurePipe` | `Name` | `public` | **aucun** | `PressurePart` (hérité) | AeccPressurePipesMgd |
| `PressureFitting` | `Name` | `public` | **aucun** | `PressurePart` (hérité) | AeccPressurePipesMgd |
| `Entity` (Civil) | `Name` | `public` | **`public`** | `Entity` | AeccDbMgd |

Autres membres « Name » (lecture seule sauf indication) :
`Part.NetworkName`, `Part.PartFamilyName`, `Part.PartSizeName`,
`Part.RefAlignmentName`, `Part.RefSurfaceName`, `Part.RuleSetStyleName`
(*get/set*), `PressurePart.NetworkName`, `PressurePart.PartFamilyName`,
`PressurePart.ReferenceAlignmentName`, `PressurePart.ReferenceSurfaceName`,
`Entity.DisplayName`, `Entity.StyleName` (*get/set*).

Conclusions :

- **Aucune méthode `SetName` / `Rename`** sur ces types.
- **Aucun `set_Name` non-public** sur `Part` ni `PressurePart` (le dump inclut
  `BindingFlags.NonPublic` sur toute la chaîne). Donc **pas de setter caché**
  managé.
- **Point clé** : `Part` et `PressurePart` ne redéfinissent que le **getter**
  (`public override string Name { get; }`). Le setter virtuel public
  `Autodesk.Civil.DatabaseServices.Entity.set_Name` existe toujours et n'est pas
  redéfini. Un setter est donc atteignable **en castant la part vers la base
  `Entity`** : `((Autodesk.Civil.DatabaseServices.Entity)part).Name = "..."`.
  Cela **compile** ; le **comportement runtime est UNVERIFIED** (peut renommer,
  être ignoré, ou lever une exception). C'est la piste (a) la plus plausible
  dans l'API managée, mais elle n'est ni documentée ni garantie.

### 7.4 Tâche 4 — `DBObject.Handle` (réflexion `acdbmgd.dll`)

| Membre | Type retour | get | set | Déclaré dans | Statut |
|---|---|---|---|---|---|
| `DBObject.Handle` | `Autodesk.AutoCAD.DatabaseServices.Handle` | `public` | **aucun** (lecture seule) | `Autodesk.AutoCAD.DatabaseServices.DBObject` | VERIFIED (réflexion) |

- `Autodesk.AutoCAD.DatabaseServices.Handle` existe ; `ToString()` y est
  **redéfini** (retour `String`).
- La **casse hexadécimale majuscule** n'est pas prouvable par métadonnées :
  **UNVERIFIED** (comportement attendu, à confirmer au runtime).

### 7.5 Synthèse (a) vs (b)

- **Aucun** setter managé documenté, **aucun** `set_Name` non-public sur
  `Part`/`PressurePart`.
- Piste (a) — cast vers la base `Autodesk.Civil.DatabaseServices.Entity` pour
  utiliser le setter public non redéfini : **UNVERIFIED** (compile, runtime
  inconnu).
- Piste (b) — l'UI passe vraisemblablement par COM/interne : **UNVERIFIED**
  (COM non documenté dans ce RAG).
- La commande UI « RenamePipeNetworkParts » n'apparaît dans le RAG que via sa
  classe de settings ; aucune API d'exécution du renommage n'y est documentée.

## 8. Renommage COM — résolu

### Voie retenue

Écriture via l'interface COM de l'objet (`DBObject.AcadObject`) avec `dynamic`
(pas de référence COM typée) :

```csharp
dynamic comPart = ((Autodesk.AutoCAD.DatabaseServices.DBObject)part).AcadObject;
comPart.Name = newName;   // newName = "<réseau>-<CON|RAC>-<handle>"
```

### Preuve

- Empirique AutoLISP : `vla-put-name` / `vla-put-property` sur `.Name` → OK.
- Runtime .NET Civil 3D 2026 : écriture, persistance (SAVE + fermeture +
  réouverture), undo (`U`) → **VALIDÉ**.

### Types couverts

`Pipe`, `PressurePipe` (`CON`) et `PressureFitting` (`RAC`). Structure et
PressureAppurtenance sont hors périmètre.

### Voies écartées

- API managée `.Name` : **lecture seule** (`Part.Name` / `PressurePart.Name`,
  override get-only) → §6.
- Réflexion : **aucun** `set_Name` non-public sur `Part` / `PressurePart` → §7.3.
- Cast vers la base `Autodesk.Civil.DatabaseServices.Entity` (setter public non
  redéfini) : non retenu (comportement non garanti).
- COM typé : pas d'interface COM pression dédiée documentée ; l'écriture via
  `IDispatch` (`dynamic`) suffit.
