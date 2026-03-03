# ---------- BUILD STAGE ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy solution & project files
COPY ["SSRS_Subscription/SSRS_Subscription.csproj", "SSRS_Subscription/"]
RUN dotnet restore "SSRS_Subscription/SSRS_Subscription.csproj"

# copy everything else
COPY . .
WORKDIR "/src/SSRS_Subscription"

RUN dotnet publish -c Release -o /app/publish


# ---------- RUNTIME STAGE ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

RUN apt-get update && apt-get install -y gss-ntlmssp

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SSRS_Subscription.dll"]