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
