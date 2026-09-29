# XdataAutoUpdate — Quick Guide

Plugin Civil 3D 2026 pour les parts de réseaux (Pressure + gravitaire).

## Chargement

    NETLOAD  →  choisir la DLL voulue

| DLL | Pour quoi |
|---|---|
| `XdataAutoUpdate.dll` | Commandes manuelles |
| `XdataAutoUpdateAuto.dll` | Mise à jour automatique au SAVE |

Les deux peuvent être chargées en même temps.

## Commandes — DLL manuelle

| Commande | Effet |
|---|---|
| `C3D_PN_XDATA` | Écrit l'XData `CIVIL3D_PN_NAME` (= nom du réseau) sur toutes les parts |
| `C3D_PN_RENAME` | Renomme les parts en `<réseau>-<CON\|RAC>-<handle>` |

Détails :

- **Portée** : tout le dessin, aucune sélection.
- **`C3D_PN_XDATA`** couvre PressurePipe, PressureFitting, PressureAppurtenance,
  Pipe, Structure.
- **`C3D_PN_RENAME`** couvre Pipe et PressurePipe (`CON`), PressureFitting
  (`RAC`). Structure et PressureAppurtenance sont ignorés.
- **Idempotent** : relancer `C3D_PN_RENAME` ne change rien.
- L'XData n'est pas touchée par `C3D_PN_RENAME`.

## Commandes — DLL auto

| Commande | Effet |
|---|---|
| `C3D_PN_XDATA_AUTO` | Exécute tout de suite XData + rename |
| `C3D_PN_XDATA_AUTO_ON` | Active l'auto (défaut au chargement) |
| `C3D_PN_XDATA_AUTO_OFF` | Désactive l'auto |

Quand l'auto est **ON**, à chaque `SAVE` : XData mise à jour **et** parts
renommées, dans une seule transaction.

## Vérifications rapides

| Vérification | Méthode |
|---|---|
| XData présente | `(entget (car (entsel)))` → chercher `CIVIL3D_PN_NAME` |
| Nom d'une part | Properties Civil 3D → champ Name |
| Auto active ? | Lancer `C3D_PN_XDATA_AUTO_ON` |
| Rename pris en compte | Rouvrir le DWG après SAVE |
