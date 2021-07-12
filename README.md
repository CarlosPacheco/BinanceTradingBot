# BinanceTradingBot
Binance Trading Bot

Build from source (self-contained):
dotnet publish -c Release -r win-x64 --self-contained true

Docker:
docker run -d --name mybot -e Config__ApiKey=your-key-here -e Config__SecretKey=your-secret-here