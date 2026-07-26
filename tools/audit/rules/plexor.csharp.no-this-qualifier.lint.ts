/**
 * plexor.no-this-qualifier -- project rule for Plexor.
 * Mirrors .agents/rules/coding/no-this-qualifier.md.
 *
 * When a constructor parameter shadows a field name, rename one of them
 * so the assignment is unambiguous. The this.<field> = <field>; pattern
 * is a code smell -- it suggests the naming was not thought through.
 *
 * Forbidden:
 *   public PasswordHash(string value)
 *   {
 *       if (!IsWellFormed(value))
 *       {
 *           throw new IdentityException(...);
 *       }
 *       this.value = value;     // <-- banned
 *   }
 *
 * Correct (rename the parameter; field keeps its name):
 *   public PasswordHash(string hash)
 *   {
 *       if (!IsWellFormed(hash))
 *       {
 *           throw new IdentityException(...);
 *       }
 *       value = hash;
 *   }
 *
 * Strategy (RE2): match lines of the shape "this.<ident> = <ident>;" --
 * the right-hand side is a bare identifier (no member access), so it
 * must be the ctor parameter being assigned into the same-named field.
 *
 * Limitation acknowledged: RE2 cannot scope the match to inside a
 * constructor body, so a class field initializer of the form
 * "this.x = y;" would fire if y happens to be x. None of Plexor's
 * existing code uses that pattern -- confirmed by self-audit grep.
 *
 * Severity is "warning" (forward-only documentation-as-code rule);
 * pre-existing violations stay until natural refactor.
 */
export default {
  id: 'plexor.no-this-qualifier',
  severity: 'warning',
  pattern: 'this\\.([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*([A-Za-z_][A-Za-z0-9_]*)\\s*;',
  globs: ['**/*.cs'],
  excludePaths: [
    '**/bin/**',
    '**/obj/**',
    '**/Migrations/**',
    '**/*.Designer.cs',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    '**/Generated/**',
    '**/*Tests*/**',
    '**/tests/**',
  ],
  message:
    'this.<field> = <field>; -- rename the parameter (or the field) so the assignment is unambiguous without the this. qualifier. See .agents/rules/coding/no-this-qualifier.md.',
  source: 'coding/no-this-qualifier.md',
  rationale:
    'The this.<field> = <field>; pattern is a code smell: the ctor parameter shadowed the field name instead of one being renamed. Common offenders include ctor parameters named value, hash, id, result, entity. The Plexor project rule prefers renaming the parameter (the field is canonical). This single line of JSDoc explains why a future maintainer should not introduce the this-qualifier; the regent rule surfaces the violation as a warning (not error) so the rule is forward-only and existing code can be fixed at the natural refactor boundary. Caveat: RE2 cannot scope to inside a constructor body, so use of this.x = y in a property getter with side effects would also fire -- but no such case exists in Plexor today.',
};
