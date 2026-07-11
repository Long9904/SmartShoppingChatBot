# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy project files
COPY ["SmartShoppingChatBot.API/SmartShoppingChatBot.API.csproj", "SmartShoppingChatBot.API/"]
COPY ["SmartShoppingChatBot.Application/SmartShoppingChatBot.Application.csproj", "SmartShoppingChatBot.Application/"]
COPY ["SmartShoppingChatBot.Infrastructure/SmartShoppingChatBot.Infrastructure.csproj", "SmartShoppingChatBot.Infrastructure/"]
COPY ["SmartShoppingChatBot.Domain/SmartShoppingChatBot.Domain.csproj", "SmartShoppingChatBot.Domain/"]

# Restore dependencies
RUN dotnet restore "SmartShoppingChatBot.API/SmartShoppingChatBot.API.csproj"

# Copy source code
COPY . .

# Build the application
RUN dotnet build "SmartShoppingChatBot.API/SmartShoppingChatBot.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SmartShoppingChatBot.API/SmartShoppingChatBot.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .


# Prompts
COPY SmartShoppingChatBot.Application/Prompts/SemanticEmbedding.md /app/prompts/SemanticEmbedding.md
COPY SmartShoppingChatBot.Application/Prompts/SectionSummary.md /app/prompts/SectionSummary.md

# Expose port
EXPOSE 80
EXPOSE 443

# Start the application
ENTRYPOINT ["dotnet", "SmartShoppingChatBot.API.dll"]