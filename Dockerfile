FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.18 AS build-env

WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ./BoardApi/*.csproj ./BoardApi/
COPY ./Core/*.csproj ./Core/
COPY ./Data/*.csproj ./Data/
RUN dotnet restore "BoardApi/Board.Api.csproj"

# Copy everything else and build
COPY . ./
RUN dotnet publish "BoardApi/Board.Api.csproj" -c Release -o publish   

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.18
WORKDIR /app

COPY --from=build-env /app/publish .

RUN apk add --no-cache tzdata

RUN apk add --no-cache icu-libs krb5-libs libgcc libintl libssl1.1 libstdc++ zlib

ENV TZ America/Argentina/Buenos_Aires

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT false

ENV ASPNETCORE_URLS http://+:80

EXPOSE 80

ENTRYPOINT ["dotnet", "Board.Api.dll"]