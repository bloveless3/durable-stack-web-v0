/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ["Inter", "Segoe UI", "sans-serif"]
      },
      colors: {
        ink: {
          strong: "#0f172a",
          default: "#23324a",
          soft: "#64748b"
        },
        surface: {
          bg: "#f5f7fb",
          panel: "#ffffff",
          border: "#dde3ef"
        },
        accent: {
          DEFAULT: "#2563eb",
          soft: "#dbeafe"
        }
      },
      boxShadow: {
        soft: "0 20px 45px -28px rgba(15, 23, 42, 0.35)"
      }
    },
  },
  plugins: [],
}
