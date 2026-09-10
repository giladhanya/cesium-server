# BigMap.Server

ASP.NET Core Web API עבור אריחי PNG של Cesium. השרת בודק קודם את ה-cache המקומי, וב-cache miss פונה לשירות `png render` ב-`http://127.0.0.1:8081`.

## הרצה

```powershell
dotnet run --project BigMap.Server
```

Endpoints:

- `GET /health`
- `GET /api/base-tiles/{z}/{x}/{y}.png`
- `GET /api/tiles/{layer}/{z}/{x}/{y}.png`
- `GET /api/tiles/layers/{z}/{x}/{y}`

כל בקשת tile חייבת לכלול את שם השכבה כפי שהוא מופיע ב־`styles.json`.
רשימת השכבות הזמינות מתקבלת מ־`GET /api/tiles/layers`.
השרת קורא את שמות השכבות ישירות מ־`GET http://127.0.0.1:8081/styles.json`.
קבצי ה-cache נשמרים בנפרד לפי שכבה: `cache/{StyleVersion}/{layer}/{z}/{x}_{y}.png`.
עבור שכבות שאינן `base`, השרת מוריד PNG ישירות מ־
`http://127.0.0.1:8081/styles/{layer}/256/{z}/{x}/{y}.png`
ומחזיר אותו דרך ה־API.

## Cesium

```ts
const provider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/base-tiles/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});

viewer.imageryLayers.addImageryProvider(provider);

const transportationProvider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/tiles/transportation/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});
viewer.imageryLayers.addImageryProvider(transportationProvider);

const waterProvider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/tiles/water/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});
viewer.imageryLayers.addImageryProvider(waterProvider);
```

הגדרות נמצאות ב-`appsettings.json`. ניתן לשנות את `TileCache:RootPath` ואת כתובת ה-renderer לפי סביבת ההרצה.
