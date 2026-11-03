FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble-chiseled@sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296
LABEL org.opencontainers.image.source=https://github.com/dahl-jar/jobranger
ENV HOME=/home/app \
    XDG_STATE_HOME=/home/app/.local/state \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/home/app/.local/state/jobranger/bundle \
    JOBRANGER_SECRET_STORE=file \
    JOBRANGER_INSTALL_METHOD=docker
COPY --chown=$APP_UID:$APP_UID home/ /home/app/
COPY jobranger /usr/local/bin/jobranger
USER $APP_UID
ENTRYPOINT ["/usr/local/bin/jobranger"]
