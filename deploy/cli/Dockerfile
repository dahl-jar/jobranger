ARG IMAGE_PLATFORM=linux/amd64

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY cli/src/Jobranger.Cli/Jobranger.Cli.csproj cli/src/Jobranger.Cli/packages.lock.json cli/src/Jobranger.Cli/
RUN dotnet restore cli/src/Jobranger.Cli/Jobranger.Cli.csproj --locked-mode
COPY cli/src/ cli/src/
RUN dotnet publish cli/src/Jobranger.Cli/Jobranger.Cli.csproj -c Release -r linux-x64 --no-restore -o /out \
    && mkdir -p /home-app/.local/state/jobranger/bundle \
    && chmod 0700 /home-app/.local/state/jobranger /home-app/.local/state/jobranger/bundle

FROM --platform=$IMAGE_PLATFORM mcr.microsoft.com/dotnet/runtime-deps:10.0.13-noble-chiseled@sha256:8d23bb82cd8345ee51738b36c6da8e2f89ded7a547bc25fa52d5cc60e37f6d7a
ENV HOME=/home/app \
    XDG_STATE_HOME=/home/app/.local/state \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/home/app/.local/state/jobranger/bundle
COPY --from=build --chown=$APP_UID:$APP_UID /home-app/ /home/app/
COPY --from=build /out/jobranger /usr/local/bin/jobranger
USER $APP_UID
ENTRYPOINT ["/usr/local/bin/jobranger"]
