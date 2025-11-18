# Immagine runtime ASP.NET
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Immagine SDK per build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia i progetti
COPY ["ChatAI.Web/ChatAI.Web.csproj", "ChatAI.Web/"]
COPY ["ChatAI.csproj", "./"]
COPY ["ChatManager.cs", "./"]
COPY ["AiHelper.cs", "./"]

# Ripristina i pacchetti
RUN dotnet restore "ChatAI.Web/ChatAI.Web.csproj"

# Copia tutto il codice
COPY . .

# Build e publish del progetto Web
WORKDIR "/src/ChatAI.Web"
RUN dotnet publish "ChatAI.Web.csproj" -c Release -o /app/publish

# Immagine finale
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render usa di solito la porta 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ChatAI.Web.dll"]
