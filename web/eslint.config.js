import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// These rules are the frontend counterpart of ResolveAI.ArchitectureTests: they keep the module
// boundaries checkable by a tool instead of by memory. Cross-directory imports use the @/ alias, so
// a rule can name a layer exactly instead of guessing at relative-path depth.
const useAlias = {
  group: ['../*', '../**'],
  message: 'Import anything outside this directory through the @/ alias.',
};

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'node_modules'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: { globals: globals.browser },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
    },
  },
  {
    files: ['src/features/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [
          useAlias,
          {
            group: ['@/features/*', '@/features/**'],
            message: 'A feature must not import another feature. Compose features in app/.',
          },
        ],
      }],
    },
  },
  {
    files: ['src/api/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [
          useAlias,
          {
            group: ['@/auth/**', '@/features/**', '@/app/**', '@/components/**'],
            message: 'The api layer owns transport only. It must not depend on identity, features or presentation.',
          },
        ],
      }],
    },
  },
  {
    files: ['src/components/**/*.{ts,tsx}', 'src/lib/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {
        patterns: [
          useAlias,
          { group: ['@/features/**', '@/app/**'], message: 'Shared presentation must not depend on a feature.' },
        ],
      }],
    },
  },
  {
    files: ['src/{app,auth,components,features,lib}/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-globals': ['error', {
        name: 'fetch',
        message: 'Only src/api may talk to the backend. Use the api client.',
      }],
    },
  },
  {
    files: ['src/**/*.test.{ts,tsx}', 'src/test/**/*.{ts,tsx}'],
    rules: { '@typescript-eslint/no-non-null-assertion': 'off' },
  },
);
