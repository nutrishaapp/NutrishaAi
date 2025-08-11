#!/usr/bin/env python3
"""
Simple HTTP server for NutrishaAI website
Serves HTML files and handles CORS for API calls
"""

import http.server
import socketserver
import os

PORT = 8080
DIRECTORY = "/Users/osamahislam/Documents/NutrishaAi"

class CORSHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)
    
    def end_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        super().end_headers()
    
    def do_OPTIONS(self):
        self.send_response(200)
        self.end_headers()
    
    def log_message(self, format, *args):
        # Custom logging
        print(f"[{self.log_date_time_string()}] {format % args}")

def run_server():
    with socketserver.TCPServer(("", PORT), CORSHTTPRequestHandler) as httpd:
        print(f"""
╔══════════════════════════════════════════════════════════╗
║                                                          ║
║            🥗 NutrishaAI Website Server                  ║
║                                                          ║
╠══════════════════════════════════════════════════════════╣
║                                                          ║
║  Server running at: http://localhost:{PORT}              ║
║                                                          ║
║  Pages available:                                        ║
║  • Homepage:     http://localhost:{PORT}/               ║
║  • Sign In:      http://localhost:{PORT}/signin.html    ║
║  • Sign Up:      http://localhost:{PORT}/signup.html    ║
║                                                          ║
║  API Backend:    http://localhost:5133                  ║
║  Swagger UI:     http://localhost:5133                  ║
║                                                          ║
║  Press Ctrl+C to stop the server                        ║
║                                                          ║
╚══════════════════════════════════════════════════════════╝
        """)
        
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n🛑 Server stopped.")
            return

if __name__ == "__main__":
    os.chdir(DIRECTORY)
    run_server()