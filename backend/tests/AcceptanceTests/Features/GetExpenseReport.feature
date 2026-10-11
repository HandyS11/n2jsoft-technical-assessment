Feature: Get an expense report

  Scenario: Getting an expense report returns its title, user and month
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I get the expense report "Juste Leblanc - Octobre 2025"
    Then the response status is 200
    And the expense report returned is
      | Title                        | User          | Month   |
      | Juste Leblanc - Octobre 2025 | Juste Leblanc | 2025-10 |

  Scenario: The report of a deleted user can still be read
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I delete the user "Juste Leblanc"
    And I get the expense report "Juste Leblanc - Octobre 2025"
    Then the response status is 200

  Scenario: Getting an unknown expense report answers not found
    When I get an unknown expense report
    Then the response is a problem with status 404

  Scenario: A malformed id answers not found
    When I request "/v1/expense-reports/not-a-guid"
    Then the response is a problem with status 404

  Scenario: Getting an expense report counts its expenses against the user's quota
    Given the following users exist
      | First name | Last name | Monthly quota |
      | Juste      | Leblanc   | 4             |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    And the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description | Amount | Status  |
      | 2025-10-02 | Hôtel       | 120.00 |         |
      | 2025-10-20 | Train       | 45.00  | Deleted |
    When I get the expense report "Juste Leblanc - Octobre 2025"
    Then the expense report returned holds 1 expenses totalling "120.00"
    And the expense report returned allows 4 expenses a month
    And the expense report returned is not read-only

  Scenario: The report of a deleted user is read-only
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I delete the user "Juste Leblanc"
    And I get the expense report "Juste Leblanc - Octobre 2025"
    Then the expense report returned is read-only
