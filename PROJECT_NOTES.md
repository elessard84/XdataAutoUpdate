# XdataAutoUpdate — Notes de projet / Handoff

Ce fichier résume l'état du projet pour reprendre le travail dans une nouvelle session.

## 1. Objectif

Plugin C# pour **Autodesk Civil 3D 2026** qui écrit/met à jour un XData
`CIVIL3D_PN_NAME` sur les parts de réseaux :

- **Pressure Networks** : `PressurePipe`, `PressureFitting`, `PressureAppurtenance`
  (valeur = `PressurePart.NetworkName`).
- **Pipe Networks gravitaires** : `Pipe` et `Structure`
  (valeur = `Network.Name` appliquée à toutes les parts du réseau).

Contraintes permanentes :

- RegApp : `CIVIL3D_PN_NAME` (code DXF `1001`), valeur chaîne (code `1000`).
- Ne **jamais** écraser les XData d'autres RegApps.
- Jamais `part.XData = null`.
- Écriture toujours via `Transaction`.
- Pas de sélection utilisateur.
- Pas d'API Autodesk inventée.

## 2. Environnement cible

| Élément | Valeur |
|---|---|
| Hôte | Civil 3D 2026 / AutoCAD 2026 |
| Runtime AutoCAD 2026 | .NET 10 (`acdbmgd.runtimeconfig.json` → `net10.0`) |
| TargetFramework | `net10.0-windows` |
| Plateforme | x64 |
| OutputType | Library (DLL) |
| Chargement | `NETLOAD` (bundle AutoLoader plus tard) |
| SDK utilisé | .NET 10.0.401 |

Assemblies Autodesk 2026 (locales) :

- `C:\Program Files\Autodesk\AutoCAD 2026\acdbmgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\acmgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\accoremgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\ACA\AecBaseMgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\C3D\AeccDbMgd.dll`
- `C:\Program Files\Autodesk\AutoCAD 2026\C3D\AeccPressurePipesMgd.dll`

Toutes référencées avec `<Private>False</Private>` (aucune copie locale).

## 3. Structure du dépôt

```
XdataAutoUpdate.csproj          # projet unique, 2 configurations -> 2 DLL
PressureNetworkXdataCommand.cs  # XdataWriter (logique partagée) + commande DLL A
AutoUpdateReactor.cs            # DLL B : reactor BeginSave + IExtensionApplication
AGENTS.md                       # règles de dev Civil 3D (à respecter)
README.md                       # quasi vide
.config/opencode/opencode.json  # config opencode
```

Fichiers générés (non versionnés) : `bin/`, `obj/`.

## 4. Les deux DLL

Un seul `.csproj`, deux configurations :

| DLL | Configuration | Constante | Assembly | Rôle |
|---|---|---|---|---|
| **A** | `Release` (ou `Debug`) | `MANUAL` | `XdataAutoUpdate.dll` | Commande manuelle `C3D_PN_XDATA` |
| **B** | `Auto` | `AUTO` | `XdataAutoUpdateAuto.dll` | Auto-update sur `Database.BeginSave` |

Le csproj définit :

```xml
<Configurations>Debug;Release;Auto</Configurations>
<PropertyGroup Condition="'$(Configuration)' != 'Auto'">
  <AssemblyName>XdataAutoUpdate</AssemblyName>
  <DefineConstants>$(DefineConstants);MANUAL</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)' == 'Auto'">
  <AssemblyName>XdataAutoUpdateAuto</AssemblyName>
  <DefineConstants>$(DefineConstants);AUTO</DefineConstants>
</PropertyGroup>
```

### Commandes de build

```powershell
# DLL A
dotnet build -c Release -p:Platform=x64
# -> bin\x64\Release\net10.0-windows\XdataAutoUpdate.dll

# DLL B
dotnet build -c Auto -p:Platform=x64
# -> bin\x64\Auto\net10.0-windows\XdataAutoUpdateAuto.dll
```

### Commandes exposées

- DLL A : `C3D_PN_XDATA`
- DLL B : `C3D_PN_XDATA_AUTO` (exécution manuelle immédiate),
  `C3D_PN_XDATA_AUTO_ON`, `C3D_PN_XDATA_AUTO_OFF`

Aucun conflit si les deux DLL sont NETLOADées.

## 5. Architecture du code

### `PressureNetworkXdataCommand.cs`

- `internal static class XdataWriter` — **logique partagée par les deux DLL** :
  - `RegAppName = "CIVIL3D_PN_NAME"`
  - `int WriteAll(Database db, CivilDocument civilDoc)` : une transaction ;
    `EnsureRegApp` puis `WritePressureNetworks` puis `WritePipeNetworks` ;
    `Commit`, sinon `Abort` + rethrow.
  - `int WritePressureNetworks(...)` : `civilDoc.GetPressurePipeNetworkIds()`
    → par réseau `GetPipeIds()` / `GetFittingIds()` / `GetAppurtenanceIds()`,
    nom lu via `PressurePart.NetworkName`.
  - `int WritePipeNetworks(...)` : `civilDoc.GetPipeNetworkIds()`
    → par réseau `GetPipeIds()` / `GetStructureIds()`,
    nom lu via `Network.Name`.
  - `EnsureRegApp(Transaction, Database)` : `RegAppTable.Has` puis éventuel
    `Add` + `AddNewlyCreatedDBObject`.
  - `SetNetworkNameXData(Autodesk.AutoCAD.DatabaseServices.DBObject, string)` :
    buffer `1001, RegAppName` + `1000, nom`, assigné à `.XData`.
    **`DBObject` est fully-qualified** (ambiguïté avec
    `Autodesk.Civil.DatabaseServices.DBObject`).
  - Les parts sont ouvertes en `OpenMode.ForWrite` (le setter XData exige
    l'écriture).
- `#if MANUAL` : `public class PressureNetworkXdataCommand`
  avec `[CommandMethod("C3D_PN_XDATA")]`.

### `AutoUpdateReactor.cs` (`#if AUTO`)

- `public class AutoUpdateReactor` :
  - Champs : `_enabled`, `_running`, `_attachedDb`.
  - `EnableInternal(Document doc)` : `EnsureRegApp(doc.Database)` dans une
    transaction normale **avant** l'attache, puis abonnement
    `DocumentActivated` (si pas déjà activé) et attache `Database.BeginSave`.
  - `DisableInternal()` : désabonne `DocumentActivated`, détache BeginSave,
    `_enabled = false`.
  - `OnBeginSave(object, DatabaseIOEventArgs)` : garde de réentrance
    `_running`, vérifie que la base sauvée est celle du document actif,
    appelle `XdataWriter.WriteAll`, erreurs écrites dans l'éditeur.
  - `OnDocumentActivated` : migre l'attache vers la base du nouveau document.
  - Commandes `C3D_PN_XDATA_AUTO`, `_ON`, `_OFF`.
- `public class AutoUpdateExtension : IExtensionApplication` :
  - `Initialize()` : si `MdiActiveDocument != null` → activation immédiate ;
    sinon abonnement à `DocumentActivated` (premier déclenchement → enable puis
    désabonnement).
  - `Terminate()` : désabonnement + `DisableInternal()`.
  - Découverte automatique par AutoCAD (pas d'attribut requis).

## 6. APIs vérifiées

### Civil 3D — RAG local `D:\RAG\Civil3D_API_2026\civapidocs_named\` (v13.8.0.292)

| Membre | Signature | Statut |
|---|---|---|
| `CivilDocument.GetPressurePipeNetworkIds` (extension) | `ObjectIdCollection` (ns `Autodesk.Civil.ApplicationServices`) | VERIFIED |
| `CivilDocument.GetPipeNetworkIds` | `ObjectIdCollection` | VERIFIED |
| `PressurePipeNetwork.GetPipeIds/GetFittingIds/GetAppurtenanceIds` | `ObjectIdCollection` | VERIFIED |
| `Network.GetPipeIds/GetStructureIds` | `ObjectIdCollection` | VERIFIED |
| `PressurePart.NetworkName` | `public string NetworkName { get; }` | VERIFIED |
| `Network.Name` | héritée de `Autodesk.Civil.DatabaseServices.Entity` : `public virtual string Name { get; set; }` | VERIFIED |
| `PressurePipe`/`PressureFitting`/`PressureAppurtenance` | `: PressurePart` (sealed) | VERIFIED |
| `Pipe`/`Structure` | `: Part` (sealed) | VERIFIED |

### AutoCAD — vérifié par réflexion (MetadataLoadContext) sur les DLL 2026

| Membre | Détail |
|---|---|
| `Database.BeginSave` | `event DatabaseIOEventHandler` |
| `DatabaseIOEventArgs` | propriété `FileName` (sender = `Database`) |
| `DocumentCollection.DocumentActivated` | `event DocumentCollectionEventHandler` (accoremgd) |
| `DocumentCollectionEventArgs.Document` | `Document` |
| `Application.DocumentManager` | statique, `DocumentCollection` (`Autodesk.AutoCAD.ApplicationServices.Core`) |
| `IExtensionApplication` | ns `Autodesk.AutoCAD.Runtime`, `void Initialize()`, `void Terminate()` |
| `DBObject.GetXDataForApplication(string)` | `ResultBuffer` |
| `DBObject.XData` | `ResultBuffer` (get/set) |
| `RegAppTable.Has(string)` / `.Add(RegAppTableRecord)` | vérifiés |
| `DxfCode.ExtendedDataRegAppName` (1001) / `ExtendedDataAsciiString` (1000) | cast `(short)` |

Fondation AutoCAD fiable (AGENTS.md) : `Application.DocumentManager.MdiActiveDocument`,
`Document.Database`, `Document.Editor`, `Transaction` / `StartTransaction` /
`GetObject` / `Commit` / `Abort`, `ObjectIdCollection`, `DBObject`, etc.

## 7. Points à valider au runtime (NON testés)

Le build et la structure sont validés statiquement ; le comportement en session
Civil 3D 2026 reste à confirmer :

1. **`BeginSave` depuis plugin NETLOADé** : confirmer que l'événement se
   déclenche et que les XData écrites sont bien incluses dans le DWG sauvé.
2. **Écriture DB pendant `BeginSave`** : ajout de `RegAppTableRecord` et
   assignation XData pendant l'événement. Si `eLockViolation` : le RegApp est
   déjà pré-enregistré par `EnableInternal`, donc normalement sans écriture new.
3. **NETLOAD de la DLL B sans commande** : doit activer l'auto-update dès le
   premier SAVE (extension `IExtensionApplication`). Vérifier aussi au démarrage
   à froid avec `DocumentActivated`.
4. **Base non active sauvée** : le handler ignore volontairement une base qui
   n'est pas celle du `MdiActiveDocument` (pas de mapping fiable vers un
   `CivilDocument`). À valider selon les scénarios WBLOCK / SAVEAS.
5. **Persistance du RegApp** : `CIVIL3D_PN_NAME` présent dans le DWG après save.

## 8. Test d'acceptation manuel

1. `NETLOAD` DLL A puis `C3D_PN_XDATA` sur un dessin avec Pressure + Pipe
   Networks → XData `CIVIL3D_PN_NAME` = nom réel du réseau sur toutes les parts.
2. Vérifier que les XData d'autres RegApps sont intactes.
3. `NETLOAD` DLL B → modifier un réseau → `SAVE` → XData mises à jour
   automatiquement avant écriture.
4. `C3D_PN_XDATA_AUTO_OFF` → un SAVE ne doit plus modifier les XData.
5. `C3D_PN_XDATA_AUTO_ON` → réactivation.

## 9. Règles de développement

Voir `AGENTS.md`. En résumé :

- Vérifier toute API Civil 3D dans le RAG local avant usage.
- Ne pas inventer d'API ; marquer `UNVERIFIED` sinon.
- Éviter les ambiguïtés de namespace (`System.Exception`,
  `Autodesk.AutoCAD.DatabaseServices.DBObject`, etc.).
- Cible `net10.0-windows`, x64, pas de `.exe`, pas de .NET 8 / Framework 4.8.
- Ne pas modifier la DLL A ni `XdataWriter` pour les tâches liées à la DLL B
  (sauf demande explicite).
- Git : pas de commit/push sans demande explicite.

## 10. État Git

- Branche : `master`, à jour avec `origin/master`.
- Dernier commit : `e5bee27 Add agents.md`.
- Modifié non commité : `AGENTS.md`.
- Nouveaux fichiers (untracked, à committer) : `.gitignore`, `README.md`,
  `PROJECT_NOTES.md`, `XdataAutoUpdate.csproj`,
  `PressureNetworkXdataCommand.cs`, `AutoUpdateReactor.cs`.
- `.gitignore` présent : `bin/`, `obj/`, `.config/`, IDE et divers sont
  désormais ignorés.
- Commit prévu : `Add project sources, notes, gitignore` (après validation).
- Aucun push sans accord explicite.
