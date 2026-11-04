FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 sqlite3 \
    && rm -rf /var/lib/apt/lists/*
USER app
RUN mkdir -p /home/app/src/dotnet
WORKDIR /home/app/src/dotnet
COPY --chown=app:app .editorconfig /home/app/src/
COPY --chown=app:app messages/ /home/app/src/messages/
COPY --chown=app:app dotnet/ /home/app/src/dotnet/
RUN dotnet restore cli/tests/Jobranger.Cli.Tests/Jobranger.Cli.Tests.csproj --locked-mode
RUN dotnet build cli/tests/Jobranger.Cli.Tests/Jobranger.Cli.Tests.csproj -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "--project", "cli/tests/Jobranger.Cli.Tests", "-c", "Release", "--no-build"]
