# BigMap.Server

ASP.NET Core Web API עבור אריחי PNG של Cesium. השרת בודק קודם את ה-cache המקומי, וב-cache miss פונה לשירות `png render` ב-`http://127.0.0.1:8081`.

## הרצה

```powershell
dotnet run --project BigMap.Server
```

Endpoints:

- `GET /health`
- `GET /api/tiles/{z}/{x}/{y}.png`

## Cesium

```ts
const provider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/tiles/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});

viewer.imageryLayers.addImageryProvider(provider);
```

הגדרות נמצאות ב-`appsettings.json`. ניתן לשנות את `TileCache:RootPath` ואת כתובת ה-renderer לפי סביבת ההרצה.
