FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt update && apt install -y clang zlib1g-dev
COPY . /app
WORKDIR /app

RUN dotnet build \
  && dotnet publish -c Release -r linux-x64 -o out 
RUN ls -la ./out

#FROM ubuntu
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0
COPY --from=build /app/out /app
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=80
CMD ["./uno-backend"]     