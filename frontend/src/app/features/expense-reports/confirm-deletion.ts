/** Asks before a physical deletion, which also removes the report's expenses. */
export function confirmDeletion(title: string): boolean {
  return window.confirm(
    `Supprimer la note de frais « ${title} » ? Ses dépenses seront supprimées avec elle.`
  );
}
