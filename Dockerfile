FROM microsoft/dotnet:2.2-runtime-alpine
COPY build config.json session.dat ./

EXPOSE 80
CMD dotnet TelegramIndex.dll
