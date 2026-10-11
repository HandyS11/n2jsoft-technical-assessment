Feature: List expense reports

  Scenario: Listing returns the most recent months first, then sorts by title
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
    And the following expense reports exist
      | User          | Month   |
      | Marc Assin    | 2024-12 |
      | Juste Leblanc | 2025-09 |
      | Marc Assin    | 2025-10 |
      | Juste Leblanc | 2025-10 |
    When I list the expense reports
    Then the response status is 200
    And the expense reports listed are
      | Title                          | Month   |
      | Juste Leblanc - Octobre 2025   | 2025-10 |
      | Marc Assin - Octobre 2025      | 2025-10 |
      | Juste Leblanc - Septembre 2025 | 2025-09 |
      | Marc Assin - Décembre 2024     | 2024-12 |

  Scenario: Listing returns no expense report when none exists
    When I list the expense reports
    Then the response status is 200
    And no expense report is listed

  Scenario: Listing returns the requested page and the total number of expense reports
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-08 |
      | Juste Leblanc | 2025-09 |
      | Juste Leblanc | 2025-10 |
    When I list page 2 of the expense reports with 2 reports per page
    Then the response status is 200
    And the expense reports listed are
      | Title                     | Month   |
      | Juste Leblanc - Août 2025 | 2025-08 |
    And the listing reports 3 expense reports in total

  Scenario Outline: Listing rejects an invalid page
    When I request "/v1/expense-reports?<query>"
    Then the response is a problem with status 400

    Examples:
      | query        |
      | page=0       |
      | pageSize=0   |
      | pageSize=101 |

  Scenario: The report of a deleted user stays listed with its title
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I delete the user "Juste Leblanc"
    And I list the expense reports
    Then the expense reports listed are
      | Title                        | Month   |
      | Juste Leblanc - Octobre 2025 | 2025-10 |

  Scenario: Renaming the user does not change the title of their reports
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I update the user "Juste Leblanc" with
      | First name | Last name | Street           | Postal code | City  | Monthly quota |
      | Justine    | Leblanc   | 12 rue de Rivoli | 75001       | Paris | 10            |
    Then the response status is 204
    When I list the expense reports
    Then the expense reports listed are
      | Title                        | Month   |
      | Juste Leblanc - Octobre 2025 | 2025-10 |
    When I get the expense report "Juste Leblanc - Octobre 2025"
    Then the expense report returned is
      | Title                        | User          | Month   |
      | Juste Leblanc - Octobre 2025 | Juste Leblanc | 2025-10 |
