# Built from the repo root so the server reads the same heroes.json as the game.
FROM heroiclabs/nakama-pluginbuilder:3.42.0 AS builder
WORKDIR /backend
COPY backend/nakama/ .
COPY backend/migrations/ ./migrations/
COPY unity/Assets/_Game/Resources/Config/heroes.json ./config/heroes.json
COPY unity/Assets/_Game/Resources/Config/buildings.json ./config/buildings.json
COPY unity/Assets/_Game/Tests/EditMode/rules_vectors.json ./testdata/rules_vectors.json
# Unit tests are part of the build, so code that fails them cannot produce an image.
RUN go vet ./... && go test -count=1 ./... && go build --trimpath --buildmode=plugin -o ./backend.so

FROM heroiclabs/nakama:3.42.0
COPY --from=builder /backend/backend.so /nakama/data/modules/backend.so
