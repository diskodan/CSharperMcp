#!/usr/bin/env python3
"""
Generate a usability review document for the CSharper MCP Server.
This script connects to the MCP server and captures real tool outputs.
"""

import asyncio
import json
import subprocess
import sys
from pathlib import Path


class McpClient:
    """Simple MCP client for testing."""

    def __init__(self, server_path: str):
        self.server_path = server_path
        self.process = None
        self.message_id = 0

    async def start(self):
        """Start the MCP server process."""
        self.process = await asyncio.create_subprocess_exec(
            "dotnet",
            "run",
            "--project",
            self.server_path,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        print("Server started", file=sys.stderr)

    async def send_request(self, method: str, params: dict = None):
        """Send a JSON-RPC request and get the response."""
        self.message_id += 1
        request = {
            "jsonrpc": "2.0",
            "id": self.message_id,
            "method": method,
            "params": params or {},
        }

        request_json = json.dumps(request) + "\n"
        print(f"Sending: {request_json.strip()}", file=sys.stderr)

        self.process.stdin.write(request_json.encode())
        await self.process.stdin.drain()

        # Read response
        response_line = await self.process.stdout.readline()
        print(f"Received: {response_line.decode().strip()}", file=sys.stderr)

        return json.loads(response_line.decode())

    async def close(self):
        """Close the server process."""
        if self.process:
            self.process.terminate()
            await self.process.wait()


async def generate_review():
    """Generate the usability review document."""
    project_root = Path(__file__).parent.parent
    server_path = project_root / "src" / "CSharperMcp.Server"
    workspace_path = project_root  # Use this repo as test workspace

    client = McpClient(str(server_path))

    try:
        await client.start()

        # Wait for server to start
        await asyncio.sleep(2)

        # Initialize protocol
        init_response = await client.send_request(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "usability-review", "version": "1.0"},
            },
        )

        print("\n# CSharper MCP Server - Usability Review\n")
        print("## Server Information\n")
        print(f"```json\n{json.dumps(init_response, indent=2)}\n```\n")

        # List tools
        tools_response = await client.send_request("tools/list")
        print("## Available Tools\n")
        print(f"Found {len(tools_response['result']['tools'])} tools:\n")

        for tool in tools_response["result"]["tools"]:
            print(f"### Tool: `{tool['name']}`\n")
            print(f"**Description:** {tool['description']}\n")
            print("**Parameters:**\n")
            print(f"```json\n{json.dumps(tool['inputSchema'], indent=2)}\n```\n")

        # Now run each tool with realistic inputs
        print("## Tool Outputs\n")

        # 1. Initialize workspace
        print("### 1. `initialize_workspace` - Load workspace\n")
        print("**Input:**\n")
        input_data = {"path": str(workspace_path)}
        print(f"```json\n{json.dumps(input_data, indent=2)}\n```\n")

        workspace_response = await client.send_request(
            "tools/call",
            {"name": "initialize_workspace", "arguments": input_data},
        )
        print("**Output:**\n")
        print(f"```json\n{json.dumps(workspace_response, indent=2)}\n```\n")

        # 2. Get diagnostics
        print("### 2. `get_diagnostics` - Get compiler errors/warnings\n")
        print("**Input:** (entire workspace, warnings and above)\n")
        input_data = {}
        print(f"```json\n{json.dumps(input_data, indent=2)}\n```\n")

        diag_response = await client.send_request(
            "tools/call", {"name": "get_diagnostics", "arguments": input_data}
        )
        print("**Output:**\n")
        print(f"```json\n{json.dumps(diag_response, indent=2)}\n```\n")

        # 3. Get symbol info
        print("### 3. `get_symbol_info` - Get symbol information\n")
        print("**Input:** (by name - System.String)\n")
        input_data = {"symbolName": "System.String"}
        print(f"```json\n{json.dumps(input_data, indent=2)}\n```\n")

        symbol_response = await client.send_request(
            "tools/call", {"name": "get_symbol_info", "arguments": input_data}
        )
        print("**Output:**\n")
        print(f"```json\n{json.dumps(symbol_response, indent=2)}\n```\n")

        print("\n---\n")
        print("Review complete!")

    finally:
        await client.close()


if __name__ == "__main__":
    asyncio.run(generate_review())
