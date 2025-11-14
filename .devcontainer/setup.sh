#!/bin/bash
set -e

echo "🚀 Starting devcontainer setup..."

# Function to print colored output
print_status() {
    echo -e "\n\033[1;34m==>\033[0m $1"
}

print_success() {
    echo -e "\033[1;32m✓\033[0m $1"
}

print_error() {
    echo -e "\033[1;31m✗\033[0m $1"
}

# Fix PowerShell permissions and add to valid shells
print_status "Configuring PowerShell..."
if [ -f /usr/bin/pwsh ]; then
    sudo chmod +x /usr/bin/pwsh /usr/share/powershell/pwsh 2>/dev/null || true

    # Add PowerShell to valid shells if not already present
    if ! grep -q "/usr/bin/pwsh" /etc/shells; then
        echo '/usr/bin/pwsh' | sudo tee -a /etc/shells > /dev/null
        print_success "Added PowerShell to valid shells"
    else
        print_success "PowerShell already in valid shells"
    fi

    # Set PowerShell as default shell for vscode user (optional)
    # Uncomment the next line if you want PowerShell as the default shell
    # sudo chsh vscode -s /usr/bin/pwsh

    print_success "PowerShell configured successfully"
else
    print_error "PowerShell not found, skipping configuration"
fi

# Update .NET workloads
print_status "Updating .NET workloads..."
sudo dotnet workload update || {
    print_error "Failed to update .NET workloads, continuing..."
}

# Clean up any permission issues with build directories
print_status "Cleaning up build artifacts..."
find . -type d -name "bin" -o -name "obj" 2>/dev/null | while read dir; do
    if [ -d "$dir" ]; then
        sudo chmod -R 755 "$dir" 2>/dev/null || true
    fi
done
print_success "Build directories cleaned"

# Install global npm packages
print_status "Installing global npm packages..."
npm install -g @anthropic-ai/claude-code 2>/dev/null || {
    print_error "Failed to install claude-code, continuing..."
}

# Initialize Claude (if needed)
print_status "Initializing Claude..."
if command -v claude &> /dev/null; then
    claude --dangerously-skip-permissions 2>/dev/null || {
        print_error "Claude initialization warning (non-critical)"
    }
    print_success "Claude initialized"
fi

# Set up Git configuration
print_status "Configuring Git..."
git config --global --add safe.directory /workspaces/content-platform 2>/dev/null || true
git config core.autocrlf false 2>/dev/null || true
print_success "Git configured"

# Display environment information
print_status "Environment Information:"
echo "  .NET Version: $(dotnet --version)"
echo "  Node Version: $(node --version)"
echo "  NPM Version: $(npm --version)"
echo "  PowerShell Version: $(pwsh --version 2>/dev/null | head -n 1 || echo 'Not available')"
echo "  User: $(whoami)"
echo "  Working Directory: $(pwd)"