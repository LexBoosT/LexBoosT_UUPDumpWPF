# UUP Dump WPF - LexBoosT ISO Downloader

Application WPF en C# avec thème sombre et effet Mica pour télécharger des ISO Windows via UUP Dump.

## Prérequis

- **.NET 8.0 SDK** ou supérieur
- **Windows 10/11** (l'effet Mica nécessite Windows 11)

## Build et Exécution

```bash
# Restaurer les packages
dotnet restore

# Build
dotnet build

# Exécuter
dotnet run
```

## Fonctionnalités

- **Recherche de builds** : Recherchez des builds Windows via l'API UUP Dump
- **Filtres** : Filtrez par type (Retail/Preview) et architecture (amd64/arm64)
- **Sélection de langue** : Choisissez la langue de l'ISO
- **Sélection d'édition** : Choisissez l'édition Windows (Home, Pro, etc.)
- **Options de téléchargement** :
  - Include Updates
  - Cleanup
  - .NET 3.5
  - ESD Compression (Solid)
- **Effet Mica** : Thème moderne avec transparence sur Windows 11
- **Thème sombre** : Interface sombre pour un confort visuel optimal

## Structure du projet

```
UUPDumpWPF/
├── Models/
│   └── Models.cs          # Classes de données (Build, Language, Edition)
├── Services/
│   ├── UUPDumpService.cs  # Service API UUP Dump
│   └── WindowsVersionService.cs  # Informations version Windows
├── App.xaml               # Ressources et styles globaux
├── MainWindow.xaml        # Interface utilisateur
├── MainWindow.xaml.cs     # Logique métier
└── UUPDumpWPF.csproj      # Projet .NET
```

## API Utilisée

- `https://api.uupdump.net/listid.php` - Liste des builds
- `https://api.uupdump.net/listlangs.php` - Langues disponibles
- `https://api.uupdump.net/listeditions.php` - Éditions disponibles

## Credits

- abbodi1406 `https://git.uupdump.net/abbodi1406`
- Kaenbyou Rin `https://git.uupdump.net/orin`

