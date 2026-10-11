Feature: Delete an expense report

  Scenario: A deleted expense report is gone for good
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-09 |
      | Juste Leblanc | 2025-10 |
    When I delete the expense report "Juste Leblanc - Octobre 2025"
    Then the response status is 204
    And the expense report "Juste Leblanc - Octobre 2025" is no longer stored
    When I list the expense reports
    Then the expense reports listed are
      | Title                          | Month   |
      | Juste Leblanc - Septembre 2025 | 2025-09 |

  Scenario Outline: A deleted expense report can no longer be read or deleted
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I delete the expense report "Juste Leblanc - Octobre 2025"
    And I <action> the expense report "Juste Leblanc - Octobre 2025"
    Then the response is a problem with status 404

    Examples:
      | action |
      | get    |
      | delete |

  Scenario: Once deleted, the report can be created again for the same month
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I delete the expense report "Juste Leblanc - Octobre 2025"
    And I create an expense report for "Juste Leblanc" for "2025-10"
    Then the response status is 201

  Scenario: Two requests deleting the same report at the same time delete it once
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When two requests delete the expense report "Juste Leblanc - Octobre 2025" at the same time
    Then the second request answers not found
    And the expense report "Juste Leblanc - Octobre 2025" is no longer stored

  Scenario: Deleting an unknown expense report answers not found
    When I delete an unknown expense report
    Then the response is a problem with status 404
