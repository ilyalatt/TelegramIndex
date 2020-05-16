FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as api-build

COPY ./src/api /src
WORKDIR /src
RUN dotnet publish --configuration Release --runtime linux-musl-x64 --output /build


FROM node:14 as web-build

COPY ./src/web /src
WORKDIR /src
# https://github.com/facebook/create-react-app/issues/7003#issuecomment-494251468
ENV TSC_WATCHFILE=UseFsEventsWithFallbackDynamicPolling
RUN yarn && yarn nps build && mv dist /build


FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-alpine

WORKDIR /app
COPY --from=api-build /build /app
COPY --from=web-build /build /app/wwwroot

EXPOSE 80
ENTRYPOINT [ "./TelegramIndex" ]
