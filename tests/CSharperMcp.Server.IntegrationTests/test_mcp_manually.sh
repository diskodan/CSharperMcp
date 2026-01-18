#!/bin/bash
FIXTURE_PATH="$(pwd)/bin/Debug/net10.0/Fixtures/SimpleSolution"
SERVER_PATH="../../src/CSharperMcp.Server/CSharperMcp.Server.csproj"

# Start server with workspace parameter and capture stderr
dotnet run --project "$SERVER_PATH" --no-build -- --workspace "$FIXTURE_PATH" 2>server_error.log &
SERVER_PID=$!

sleep 2

# Send initialize
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

sleep 1

# Send initialized notification  
echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'

sleep 1

# Send tools/list
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

sleep 2

# Kill server
kill $SERVER_PID 2>/dev/null

# Show errors
echo "=== Server stderr ==="
cat server_error.log
