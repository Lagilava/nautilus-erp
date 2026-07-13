/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Warm-paper canvas and deep-pine ink — clean, open, natural.
        canvas: '#F7F5F0',
        surface: '#FFFFFF',
        ink: {
          DEFAULT: '#14312B', // deep pine
          soft: '#3A4C46',
          muted: '#697974',
        },
        // Lagoon teal — the restrained primary.
        lagoon: {
          50: '#E9F3F1',
          100: '#CFE6E1',
          200: '#A3D0C7',
          300: '#6FB4A8',
          400: '#3F9385',
          500: '#0E7367', // primary
          600: '#0B5A50',
          700: '#0A4740',
          800: '#083A34',
          900: '#062B27',
        },
        // Azure — the ocean-blue drawn straight from the Nautilus mark, used for
        // gradients and accents that echo the shell-and-wave logo.
        azure: {
          50: '#EEF4FB',
          100: '#D8E6F5',
          200: '#B0CCEA',
          300: '#7FA9DA',
          400: '#4A82C4',
          500: '#2C63AB', // logo mid-blue
          600: '#1E4A8A',
          700: '#173A6E',
          800: '#12305C',
          900: '#0E2547', // logo deep navy
        },
        // Sand-gold — used sparingly for emphasis.
        sand: {
          100: '#F5ECD8',
          300: '#E2C88A',
          500: '#B98B3E',
          700: '#8C6526',
        },
        line: '#E7E3DA', // warm hairline border
        success: '#2F855A',
        warning: '#B7791F',
        danger: '#C0392B',
      },
      fontFamily: {
        // Serif display for titles; clean sans for the interface.
        display: ['"Source Serif 4"', 'Newsreader', 'Georgia', 'serif'],
        sans: ['Inter', 'system-ui', '-apple-system', 'Segoe UI', 'sans-serif'],
      },
      boxShadow: {
        card: '0 1px 2px rgba(20, 49, 43, 0.04), 0 1px 3px rgba(20, 49, 43, 0.06)',
        raised: '0 4px 16px rgba(20, 49, 43, 0.08)',
      },
    },
  },
  plugins: [],
};
