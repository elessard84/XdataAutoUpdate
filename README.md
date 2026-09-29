# XdataAutoUpdate

Plugin Civil 3D 2026 (.NET 10, x64) qui :

- écrit un XData `CIVIL3D_PN_NAME` sur les parts de réseaux,
- renomme les parts au format `<réseau>-<CON|RAC>-<handle>`.

## Documentation

| Fichier | Public |
|---|---|
| `QUICK_GUIDE.md` | Utilisateur Civil 3D (commandes, usage) |
| `PROJECT_NOTES.md` | Handoff technique, état du projet |
| `EXTENSIBILITY_NOTES.md` | Reconnaissance API, cadrage |
| `AGENTS.md` | Règles de dev Civil 3D |

## Deux DLL, un seul projet

| DLL | Configuration | Rôle |
|---|---|---|
| `XdataAutoUpdate.dll` | `Release` | Commandes manuelles |
| `XdataAutoUpdateAuto.dll` | `Auto` | Mise à jour auto sur `Database.BeginSave` |

## Build

    dotnet build -c Release -p:Platform=x64
    dotnet build -c Auto    -p:Platform=x64

## Architecture

- `PressureNetworkXdataCommand.cs` — `XdataWriter` + commande `C3D_PN_XDATA`
- `RenameCommand.cs` — `CivilPartRenamer` + commande `C3D_PN_RENAME`
- `AutoUpdateReactor.cs` — reactor `BeginSave` + `IExtensionApplication`

## Point technique notable

L'API managée Civil expose `.Name` des parts en **lecture seule**.
Le renommage passe par l'interface COM de l'objet
(`DBObject.AcadObject`, écriture via `dynamic`) — validé empiriquement
en session Civil 3D 2026 (écriture, persistance, undo).

## Règles de dev

Voir `AGENTS.md`. En résumé : toute API Civil 3D doit être vérifiée dans
le RAG local avant usage ; pas d'API inventée ; cible `net10.0-windows`,
x64.
