# GymOS API — container image for Railway / Render / Fly / any container host.
#
# Build context is the REPOSITORY ROOT, not backend/, because the csproj files reference each other
# by relative path across the solution. Build it with:
#
#   docker build -f backend/Dockerfile -t gymos-api .
#
# Two stages: the SDK image compiles and publishes, and only the published output is copied into the
# much smaller ASP.NET runtime image. The SDK is ~800MB and has no business being in a running
# container.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Directory.Build.props FIRST, and it is not optional. MSBuild walks up from each csproj looking for
# it, so a build without it silently compiles every project with ImplicitUsings and Nullable OFF —
# which is not a warning, it is several thousand missing-using errors. It builds fine locally because
# the file is simply there on disk; in a container it only exists if copied. This is what failed the
# first Railway build.
COPY backend/Directory.Build.props backend/

# Then the project files alone, and restore against them. This layer only invalidates when a csproj
# changes, so ordinary source edits reuse the cached NuGet restore instead of re-downloading every
# package on every deploy.
COPY backend/src/GymOS.Domain/GymOS.Domain.csproj                 backend/src/GymOS.Domain/
COPY backend/src/GymOS.Shared/GymOS.Shared.csproj                 backend/src/GymOS.Shared/
COPY backend/src/GymOS.Application/GymOS.Application.csproj       backend/src/GymOS.Application/
COPY backend/src/GymOS.Infrastructure/GymOS.Infrastructure.csproj backend/src/GymOS.Infrastructure/
COPY backend/src/GymOS.API/GymOS.API.csproj                       backend/src/GymOS.API/
RUN dotnet restore backend/src/GymOS.API/GymOS.API.csproj

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/GymOS.API/GymOS.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Local disk storage default (Storage:Provider=Local) writes here. On a container host this is
# ephemeral — it vanishes on redeploy — which is fine for uploads nobody depends on yet and is the
# reason to move Storage:Provider to S3 before anything important is uploaded.
RUN mkdir -p /app/App_Data/uploads

# Documentation only; the host injects PORT and Program.cs binds 0.0.0.0:$PORT from it. Kestrel's own
# default is 8080 in these images, which is what this matches when PORT is absent.
EXPOSE 8080

ENTRYPOINT ["dotnet", "GymOS.API.dll"]
