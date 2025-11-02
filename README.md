### GizmoApp
App for Comunication with Homeassistant Assistpipeline

## you have to add a config.json file in recources with following content 
# for https:
```bash
 "HA_BASE_URL": "wss://Your Homeassistant Url:HA Port",
 "HA_TOKEN": "Your Homeassistant Longlived Accesstoken"
```

# for http:
```bash
 "HA_BASE_URL": "ws://homeassistant.local:HA Port",
 "HA_TOKEN": "Your Homeassistant Longlived Accesstoken"
```

## you have to add a .env fine in the mainDirectory with following content 

# for https:
```bash
 HA_BASE_URL=wss://Your Homeassistant Url:HA Port
 HA_TOKEN=Your Homeassistant Longlived Accesstoken
```

# for http:
```bash
 HA_BASE_URL=ws://homeassistant.local:HA Port
 HA_TOKEN=Your Homeassistant Longlived Accesstoken
```

## now you have to install the dependencies:

```bash
dotnet clean

dotnet add package Websocket.client
dotnet add package dotenv.net
dotnet add package CommunityToolkit.Mvvm
dotnet add package CommunityToolkit.Maui

```