ARG IMAGE_PLATFORM=linux/amd64

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY contracts/src/Jobranger.Contracts/Jobranger.Contracts.csproj contracts/src/Jobranger.Contracts/packages.lock.json contracts/src/Jobranger.Contracts/
COPY cli/src/Jobranger.Cli/Jobranger.Cli.csproj cli/src/Jobranger.Cli/packages.lock.json cli/src/Jobranger.Cli/
RUN dotnet restore cli/src/Jobranger.Cli/Jobranger.Cli.csproj --locked-mode
COPY contracts/payload.schema.json contracts/
COPY contracts/unicode/idna-hosts-16.0.0.json contracts/unicode/
COPY contracts/src/ contracts/src/
COPY cli/src/ cli/src/
RUN dotnet publish cli/src/Jobranger.Cli/Jobranger.Cli.csproj -c Release -r linux-x64 --no-restore -o /out \
    && mkdir -p /home-app/.local/state/jobranger/bundle \
    && chmod 0700 /home-app/.local/state/jobranger /home-app/.local/state/jobranger/bundle

FROM --platform=$IMAGE_PLATFORM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble-chiseled@sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296
ENV HOME=/home/app \
    XDG_STATE_HOME=/home/app/.local/state \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/home/app/.local/state/jobranger/bundle
COPY --from=build --chown=$APP_UID:$APP_UID /home-app/ /home/app/
COPY --from=build /out/jobranger /usr/local/bin/jobranger
USER $APP_UID
ENTRYPOINT ["/usr/local/bin/jobranger"]
