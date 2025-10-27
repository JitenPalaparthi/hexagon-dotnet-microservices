# GlobalConfig + Feature Flags Demo (.NET 9)

This demo shows how to use a **shared global config file** + **feature flags** in a .NET 9 app.

## Run

```bash
dotnet build
dotnet run --project DemoApp
```

Then visit:
- http://localhost:5050/ → shows combined global + local config
- http://localhost:5050/welcome → toggled by FeatureManagement:ShowWelcome
- http://localhost:5050/discount → toggled by FeatureManagement:EnableDiscountBanner

### Live reload
Edit `_global/appsettings.shared.json` while running — changes apply instantly!

### Example shared config

```json
{
  "Company": { "Name": "Contoso Ltd", "Environment": "Global" },
  "FeatureManagement": { "ShowWelcome": true, "EnableDiscountBanner": false }
}
```
