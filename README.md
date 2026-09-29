# XdataAutoUpdate

Plugin Civil 3D 2026 (.NET 10, x64) qui écrit/met à jour un XData
`CIVIL3D_PN_NAME` sur les parts de Pressure Networks et Pipe Networks.

## Deux DLL, un seul projet

| DLL | Configuration | Rôle |
|---|---|---|
| `XdataAutoUpdate.dll` | `Release` | Commande manuelle `C3D_PN_XDATA` |
| `XdataAutoUpdateAuto.dll` | `Auto` | Auto-update sur `Database.BeginSave` |

## Build

    dotnet build -c Release -p:Platform=x64
    dotnet build -c Auto    -p:Platform=x64

## Utilisation

- DLL A : `NETLOAD` puis `C3D_PN_XDATA`.
- DLL B : `NETLOAD`, auto-update actif ; commandes `C3D_PN_XDATA_AUTO`,
  `_ON`, `_OFF`.

## Détails

Voir `PROJECT_NOTES.md` (état du projet) et `AGENTS.md` (règles de dev).
