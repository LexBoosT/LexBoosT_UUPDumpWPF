# Changelog

## v1.2 - March 13, 2026

### 🐛 Bug Fixes
- **ARM64 Support**: Fixed arm64 builds not appearing in search results
- **Architecture API**: Now using UUP Dump API directly to fetch architecture (faster and more reliable)

### ✨ New Features
- **Real-time Filtering**: Architecture (amd64/arm64) and Type (Retail/Preview) filters apply instantly without re-searching
- **Color-coded Display**: AMD64 in red and ARM64 in green with brackets `[AMD64]` / `[ARM64]`
- **Custom ISO Naming**: Format `Windows{Version}_{Edition}_{FeatureVersion}_{BuildNumber}_{Language}.iso`
  - Example: `Windows11_PRO_25H2_26200.8106_fr-fr.iso`

### 🎨 UI Improvements
- **Rounded Corners**: All panels, buttons, lists, and text fields now have 8px rounded corners
- **Architecture Label**: Now bold and underlined

### ⚡ Performance
- **API Calls**: Reduced API requests (architecture included in initial response)
- **User Experience**: Instant filtering for smoother navigation
